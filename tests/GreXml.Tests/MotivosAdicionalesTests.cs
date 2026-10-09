using System.Xml.Linq;
using GreXml.Core.Envio;
using GreXml.Core.Modelo;
using GreXml.Core.Validacion;
using GreXml.Core.Xml;
using R = GreXml.Core.Validacion.CatalogoReglas;

namespace GreXml.Tests;

/// <summary>Motivos 02 (compra) y 04 (traslado entre establecimientos): reglas, XML y ciclo completo.</summary>
public class MotivosAdicionalesTests
{
    private static readonly XNamespace Cac = GeneradorXml.Cac;
    private static readonly XNamespace Cbc = GeneradorXml.Cbc;

    private readonly GuiaValidador _validador = GuiaValidador.ConMotivosSoportados();

    public static TheoryData<string> GuiasValidas => ["compra", "traslado"];

    private static GuiaRemision Guia(string nombre) => nombre == "compra" ? GuiaEjemplo.Compra() : GuiaEjemplo.TrasladoEntreEstablecimientos();

    [Theory]
    [MemberData(nameof(GuiasValidas))]
    public void GuiaValida_SinErrores_YXmlCumpleElEsquema(string nombre)
    {
        var guia = Guia(nombre);

        Assert.Empty(_validador.Validar(guia));
        Assert.Empty(ValidadorXsd.Validar(GeneradorXml.Generar(guia)));
    }

    [Theory]
    [MemberData(nameof(GuiasValidas))]
    public void DestinatarioDistintoDelRemitente_Error2554(string nombre)
    {
        var guia = Guia(nombre) with
        {
            Destinatario = new Destinatario(TiposDocumento.Ruc, GuiaEjemplo.RucDestinatario, "OTRA EMPRESA"),
        };

        AssertUnError(guia, R.DestinatarioDebeSerRemitente, "destinatario.numeroDocumento");
    }

    // ---------- 02 Compra ----------

    [Fact]
    public void Compra_SinProveedor_Error4375()
    {
        AssertUnError(GuiaEjemplo.Compra() with { Proveedor = null }, R.ProveedorObligatorio, "proveedor");
    }

    [Fact]
    public void Compra_ProveedorEsElRemitente_Error4053()
    {
        var guia = GuiaEjemplo.Compra() with { Proveedor = new Contribuyente(GuiaEjemplo.RucRemitente, "DISTRIBUIDORA EJEMPLO SAC") };

        AssertUnError(guia, R.ProveedorIgualARemitente, "proveedor.ruc");
    }

    [Fact]
    public void Compra_ProveedorConDatosInvalidos_UnErrorPorCampo()
    {
        var guia = GuiaEjemplo.Compra() with { Proveedor = new Contribuyente("20100000085", " ") };

        var errores = _validador.Validar(guia);

        Assert.Equal(
            [("proveedor.ruc", R.RucProveedorInvalido.Codigo), ("proveedor.razonSocial", R.RazonSocialProveedorObligatoria.Codigo)],
            errores.Select(e => (e.Campo, e.Codigo)));
    }

    [Fact]
    public void Compra_XmlIncluyeAlProveedorEnSellerSupplierParty()
    {
        var raiz = GeneradorXml.Generar(GuiaEjemplo.Compra()).Root!;
        var proveedor = raiz.Element(Cac + "SellerSupplierParty")!.Element(Cac + "Party")!;

        Assert.Equal(GuiaEjemplo.RucProveedor, proveedor.Element(Cac + "PartyIdentification")!.Element(Cbc + "ID")!.Value);
        Assert.Equal("FABRICA INVENTADA SA", proveedor.Element(Cac + "PartyLegalEntity")!.Element(Cbc + "RegistrationName")!.Value);
        Assert.Equal("02", raiz.Element(Cac + "Shipment")!.Element(Cbc + "HandlingCode")!.Value);
        Assert.Equal("Compra", raiz.Element(Cac + "Shipment")!.Element(Cbc + "HandlingInstructions")!.Value);
    }

    // ---------- Proveedor en motivos que no lo llevan ----------

    [Theory]
    [InlineData(MotivosTraslado.Venta)]
    [InlineData(MotivosTraslado.TrasladoEntreEstablecimientos)]
    public void OtrosMotivos_ConProveedor_Error4054(string motivo)
    {
        var guia = (motivo == MotivosTraslado.Venta ? GuiaEjemplo.VentaConTransportePrivado() : GuiaEjemplo.TrasladoEntreEstablecimientos())
            with { Proveedor = new Contribuyente(GuiaEjemplo.RucProveedor, "FABRICA INVENTADA SA") };

        AssertUnError(guia, R.ProveedorNoCorresponde, "proveedor");
    }

    [Fact]
    public void Venta_XmlSinSellerSupplierParty()
    {
        Assert.Null(GeneradorXml.Generar(GuiaEjemplo.VentaConTransportePrivado()).Root!.Element(Cac + "SellerSupplierParty"));
    }

    // ---------- 04 Traslado entre establecimientos ----------

    [Theory]
    [InlineData("150101", "AV. LOS EJEMPLOS 123, LIMA")]
    [InlineData("150101", "av. los ejemplos  123, lima")] // misma dirección escrita distinto
    public void Traslado_PartidaIgualALlegada_Error(string ubigeo, string direccion)
    {
        var guia = GuiaEjemplo.TrasladoEntreEstablecimientos() with { Llegada = new PuntoTraslado(ubigeo, direccion) };

        var error = Assert.Single(_validador.Validar(guia));

        Assert.Equal(R.PartidaIgualALlegada.Codigo, error.Codigo);
        Assert.Null(error.CodigoSunat); // regla propia: SUNAT no tiene un código para este caso
    }

    [Fact]
    public void Traslado_MismaDireccionEnOtroDistrito_SeAcepta()
    {
        var guia = GuiaEjemplo.TrasladoEntreEstablecimientos() with { Llegada = new PuntoTraslado("150103", "AV. LOS EJEMPLOS 123, LIMA") };

        Assert.Empty(_validador.Validar(guia));
    }

    [Fact]
    public void Traslado_XmlConMotivo04YSuDescripcion()
    {
        var envio = GeneradorXml.Generar(GuiaEjemplo.TrasladoEntreEstablecimientos()).Root!.Element(Cac + "Shipment")!;

        Assert.Equal("04", envio.Element(Cbc + "HandlingCode")!.Value);
        Assert.Equal("Traslado entre establecimientos de la misma empresa", envio.Element(Cbc + "HandlingInstructions")!.Value);
    }

    // ---------- El resto de la tubería no cambió ----------

    [Theory]
    [MemberData(nameof(GuiasValidas))]
    public async Task CicloCompleto_FirmadaEnviadaYAceptada(string nombre)
    {
        using var certificado = Certificados.Crear("CN=Remitente de prueba");
        var sunat = new EnvioSunatSimulado(certificado, TimeProvider.System);
        var xml = FirmadorXml.Firmar(GeneradorXml.GenerarTexto(Guia(nombre)), certificado);

        var ticket = await sunat.EnviarAsync(EmpaquetadorGre.Empaquetar(xml).ComoSolicitud());
        var estado = await sunat.EsperarAsync(ticket.NumTicket, maxIntentos: 3, TimeSpan.Zero, TimeProvider.System);

        Assert.Equal(EstadoCdr.Aceptada, LectorCdr.Leer(estado.Cdr!).Estado);
    }

    private void AssertUnError(GuiaRemision guia, Regla regla, string campo)
    {
        var error = Assert.Single(_validador.Validar(guia));
        Assert.Equal(regla.Codigo, error.Codigo);
        Assert.Equal(regla.CodigoSunat, error.CodigoSunat);
        Assert.Equal(campo, error.Campo);
    }
}
