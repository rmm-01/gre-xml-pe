using GreXml.Core.Modelo;
using GreXml.Core.Validacion;
using R = GreXml.Core.Validacion.CatalogoReglas;

namespace GreXml.Tests;

public class GuiaValidadorTests
{
    private readonly GuiaValidador _validador = GuiaValidador.ConMotivosSoportados();

    [Fact]
    public void GuiaValida_TransportePrivado_SinErrores()
    {
        Assert.Empty(_validador.Validar(GuiaEjemplo.VentaConTransportePrivado()));
    }

    [Fact]
    public void GuiaValida_TransportePublico_SinErrores()
    {
        Assert.Empty(_validador.Validar(GuiaEjemplo.VentaConTransportePublico()));
    }

    [Fact]
    public void DevuelveTodosLosErrores_NoSoloElPrimero()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with { Serie = "F001", Numero = 0, PesoBrutoTotal = 0 };

        var codigos = _validador.Validar(guia).Select(e => e.Codigo);

        Assert.Equal([R.SerieInvalida.Codigo, R.NumeroInvalido.Codigo, R.PesoBrutoNoPositivo.Codigo], codigos);
    }

    [Theory]
    [InlineData("F001")] // serie de factura
    [InlineData("T01")]
    [InlineData("T0001")]
    [InlineData("t001")]
    [InlineData("")]
    public void Serie_Invalida(string serie)
    {
        AssertUnError(GuiaEjemplo.VentaConTransportePrivado() with { Serie = serie }, R.SerieInvalida, "serie");
    }

    [Theory]
    [InlineData("TA01")]
    [InlineData("T999")]
    public void Serie_Valida(string serie)
    {
        Assert.Empty(_validador.Validar(GuiaEjemplo.VentaConTransportePrivado() with { Serie = serie }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100_000_000)]
    public void Numero_FueraDeRango(int numero)
    {
        AssertUnError(GuiaEjemplo.VentaConTransportePrivado() with { Numero = numero }, R.NumeroInvalido, "numero");
    }

    [Fact]
    public void Remitente_RucConDigitoVerificadorIncorrecto()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with { Remitente = new Contribuyente("20100000051", "EMPRESA") };

        AssertUnError(guia, R.RucRemitenteInvalido, "remitente.ruc");
    }

    [Fact]
    public void Remitente_SinRazonSocial()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with { Remitente = new Contribuyente(GuiaEjemplo.RucRemitente, " ") };

        AssertUnError(guia, R.RazonSocialRemitenteObligatoria, "remitente.razonSocial");
    }

    [Theory]
    [InlineData(TiposDocumento.Dni, "1234567")]
    [InlineData(TiposDocumento.Ruc, "12345678")] // DNI declarado como RUC
    [InlineData(TiposDocumento.Pasaporte, "AB-12345")]
    public void Destinatario_NumeroNoCorrespondeAlTipo(string tipo, string numero)
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with { Destinatario = new Destinatario(tipo, numero, "CLIENTE") };

        AssertUnError(guia, R.DocumentoDestinatarioInvalido, "destinatario.numeroDocumento");
    }

    [Theory]
    [InlineData(TiposDocumento.Dni, "12345678")] // venta a una persona
    [InlineData(TiposDocumento.CarneExtranjeria, "001234567")]
    [InlineData(TiposDocumento.Pasaporte, "AB1234567")]
    public void Destinatario_OtrosDocumentosValidos(string tipo, string numero)
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with { Destinatario = new Destinatario(tipo, numero, "CLIENTE") };

        Assert.Empty(_validador.Validar(guia));
    }

    [Fact]
    public void Destinatario_TipoDeDocumentoDesconocido()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with { Destinatario = new Destinatario("9", "123", "CLIENTE") };

        AssertUnError(guia, R.TipoDocumentoDestinatarioInvalido, "destinatario.tipoDocumento");
    }

    [Fact]
    public void Destinatario_SinNombre()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with
        {
            Destinatario = new Destinatario(TiposDocumento.Ruc, GuiaEjemplo.RucDestinatario, ""),
        };

        AssertUnError(guia, R.NombreDestinatarioObligatorio, "destinatario.nombre");
    }

    [Fact]
    public void Motivo_NoSoportado()
    {
        AssertUnError(GuiaEjemplo.VentaConTransportePrivado() with { MotivoTraslado = "99" }, R.MotivoNoSoportado, "motivoTraslado");
    }

    [Fact]
    public void Observaciones_250Caracteres_SeAceptan_251No()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado();

        Assert.Empty(_validador.Validar(guia with { Observaciones = new string('x', 250) }));
        AssertUnError(guia with { Observaciones = new string('x', 251) }, R.ObservacionesDemasiadoLargas, "observaciones");
    }

    [Fact]
    public void FechaInicio_AnteriorAEmision()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with { FechaInicioTraslado = new DateOnly(2026, 10, 8) };

        AssertUnError(guia, R.FechaInicioAnteriorAEmision, "fechaInicioTraslado");
    }

    [Fact]
    public void FechaInicio_MismoDiaQueEmision_SeAcepta()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado();

        Assert.Empty(_validador.Validar(guia with { FechaInicioTraslado = guia.FechaEmision }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PesoBruto_NoPositivo(decimal peso)
    {
        AssertUnError(GuiaEjemplo.VentaConTransportePrivado() with { PesoBrutoTotal = peso }, R.PesoBrutoNoPositivo, "pesoBrutoTotal");
    }

    [Theory]
    [InlineData("TNE")]
    [InlineData("kgm")]
    public void UnidadPeso_DistintaDeKgm(string unidad)
    {
        AssertUnError(GuiaEjemplo.VentaConTransportePrivado() with { UnidadPeso = unidad }, R.UnidadPesoInvalida, "unidadPeso");
    }

    [Theory]
    [InlineData("15010")]
    [InlineData("1501011")]
    [InlineData("15O101")] // letra O en lugar de cero
    public void Ubigeo_Invalido(string ubigeo)
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with { Llegada = new PuntoTraslado(ubigeo, "CALLE 1") };

        AssertUnError(guia, R.UbigeoInvalido, "llegada.ubigeo");
    }

    [Fact]
    public void Partida_SinDireccion()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with { Partida = new PuntoTraslado("150101", "") };

        AssertUnError(guia, R.DireccionObligatoria, "partida.direccion");
    }

    [Fact]
    public void Bienes_ListaVacia()
    {
        AssertUnError(GuiaEjemplo.VentaConTransportePrivado() with { Bienes = [] }, R.SinBienes, "bienes");
    }

    [Fact]
    public void Bienes_ErrorEnElSegundo_IndicaSuPosicion()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with
        {
            Bienes = [new Bien("BIEN CORRECTO", 1, "NIU"), new Bien("BIEN SIN CANTIDAD", 0, "NIU")],
        };

        AssertUnError(guia, R.CantidadBienNoPositiva, "bienes[1].cantidad");
    }

    [Fact]
    public void Bien_Descripcion500Caracteres_SeAcepta_501No()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado();

        Assert.Empty(_validador.Validar(guia with { Bienes = [new Bien(new string('x', 500), 1, "NIU")] }));
        AssertUnError(guia with { Bienes = [new Bien(new string('x', 501), 1, "NIU")] },
            R.DescripcionBienInvalida, "bienes[0].descripcion");
    }

    [Fact]
    public void Bien_SinUnidadDeMedida()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with { Bienes = [new Bien("BIEN", 1, "")] };

        AssertUnError(guia, R.UnidadMedidaBienObligatoria, "bienes[0].unidadMedida");
    }

    [Fact]
    public void TransportePublico_SinTransportista()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with
        {
            Transporte = new Transporte { Modalidad = ModalidadTransporte.Publico },
        };

        AssertUnError(guia, R.TransportistaObligatorio, "transporte.transportista");
    }

    [Fact]
    public void TransportePublico_RucDelTransportistaInvalido()
    {
        AssertUnError(ConTransportista("20100000077"), R.RucTransportistaInvalido, "transporte.transportista.ruc");
    }

    [Fact]
    public void TransportePublico_TransportistaEsElRemitente()
    {
        AssertUnError(ConTransportista(GuiaEjemplo.RucRemitente), R.TransportistaIgualARemitente, "transporte.transportista.ruc");
    }

    [Fact]
    public void TransportePrivado_SinVehiculosNiConductores()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with
        {
            Transporte = new Transporte { Modalidad = ModalidadTransporte.Privado },
        };

        var errores = _validador.Validar(guia);

        Assert.Equal([R.SinVehiculos.Codigo, R.SinConductores.Codigo], errores.Select(e => e.Codigo));
    }

    [Theory]
    [InlineData("ABC-123")]
    [InlineData("abc123")]
    [InlineData("AB12")]
    public void TransportePrivado_PlacaInvalida(string placa)
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado();
        guia = guia with { Transporte = guia.Transporte with { Vehiculos = ["ABC123", placa] } };

        AssertUnError(guia, R.PlacaInvalida, "transporte.vehiculos[1]");
    }

    [Fact]
    public void TransportePrivado_ConductorIncompleto_UnErrorPorCampo()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado();
        guia = guia with
        {
            Transporte = guia.Transporte with { Conductores = [new Conductor(TiposDocumento.Dni, "123", "", " ", "")] },
        };

        var errores = _validador.Validar(guia);

        Assert.Equal(
            [
                ("transporte.conductores[0].numeroDocumento", "2569"),
                ("transporte.conductores[0].nombres", "3360"),
                ("transporte.conductores[0].apellidos", "3361"),
                ("transporte.conductores[0].licencia", "2572"),
            ],
            errores.Select(e => (e.Campo, e.CodigoSunat)));
    }

    [Fact]
    public void Venta_DestinatarioEsElRemitente()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with
        {
            Destinatario = new Destinatario(TiposDocumento.Ruc, GuiaEjemplo.RucRemitente, "DISTRIBUIDORA EJEMPLO SAC"),
        };

        AssertUnError(guia, R.VentaDestinatarioIgualARemitente, "destinatario.numeroDocumento");
    }

    [Fact]
    public void Error_IncluyeElCodigoSunatEquivalente()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with { FechaInicioTraslado = new DateOnly(2026, 1, 1) };

        var error = Assert.Single(_validador.Validar(guia));

        Assert.Equal("GRE-201", error.Codigo);
        Assert.Equal("3343", error.CodigoSunat);
    }

    [Fact]
    public void Catalogo_CodigosPropiosNoSeRepiten()
    {
        var codigos = typeof(R).GetFields().Select(f => ((Regla)f.GetValue(null)!).Codigo).ToList();

        Assert.Equal(codigos.Count, codigos.Distinct().Count());
    }

    private static GuiaRemision ConTransportista(string ruc) => GuiaEjemplo.VentaConTransportePublico() with
    {
        Transporte = new Transporte
        {
            Modalidad = ModalidadTransporte.Publico,
            Transportista = new Contribuyente(ruc, "TRANSPORTES"),
        },
    };

    /// <summary>La guía tiene exactamente un error: el de la regla indicada, en el campo indicado.</summary>
    private void AssertUnError(GuiaRemision guia, Regla regla, string campo)
    {
        var error = Assert.Single(_validador.Validar(guia));
        Assert.Equal(regla.Codigo, error.Codigo);
        Assert.Equal(regla.CodigoSunat, error.CodigoSunat);
        Assert.Equal(campo, error.Campo);
    }
}
