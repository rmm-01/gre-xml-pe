using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using GreXml.Core.Envio;
using GreXml.Core.Modelo;
using GreXml.Core.Xml;

namespace GreXml.Tests;

/// <summary>Empaquetado, envío simulado, espera del ticket y lectura del CDR: el ciclo completo sin red.</summary>
public sealed class EnvioTests : IDisposable
{
    private const string NombreBase = "20100000050-09-T001-1";

    private readonly X509Certificate2 _certificado = Certificados.Crear("CN=Remitente de prueba");
    private readonly EnvioSunatSimulado _sunat;

    public EnvioTests()
    {
        _sunat = new EnvioSunatSimulado(_certificado, TimeProvider.System);
    }

    private string XmlFirmado(GuiaRemision? guia = null) =>
        FirmadorXml.Firmar(GeneradorXml.GenerarTexto(guia ?? GuiaEjemplo.VentaConTransportePrivado()), _certificado);

    // ---------- Empaquetado ----------

    [Fact]
    public void Paquete_NombreSaleDelXml()
    {
        var paquete = EmpaquetadorGre.Empaquetar(XmlFirmado());

        Assert.Equal("20100000050-09-T001-1.zip", paquete.NombreArchivo);
    }

    [Fact]
    public void Paquete_UnSoloXml_ConLosMismosBytes_YLaFirmaSigueValida()
    {
        var xml = XmlFirmado();
        var paquete = EmpaquetadorGre.Empaquetar(xml);

        using var zip = new ZipArchive(new MemoryStream(paquete.Zip));
        var entrada = Assert.Single(zip.Entries);
        Assert.Equal(NombreBase + ".xml", entrada.FullName);
        using var lector = new StreamReader(entrada.Open(), Encoding.UTF8);
        var extraido = lector.ReadToEnd();
        Assert.Equal(xml, extraido);
        Assert.True(FirmadorXml.Verificar(extraido, _certificado));
    }

    [Fact]
    public void Paquete_HashSha256EnHexadecimal_YBase64DelZip()
    {
        var paquete = EmpaquetadorGre.Empaquetar(XmlFirmado());
        var solicitud = paquete.ComoSolicitud();

        Assert.Matches("^[0-9a-f]{64}$", solicitud.HashZip);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(paquete.Zip)), solicitud.HashZip);
        Assert.Equal(paquete.Zip, Convert.FromBase64String(solicitud.ArcGreZip));
    }

    // ---------- Ciclo completo ----------

    [Fact]
    public async Task GuiaCorrecta_PrimeroEnProceso_LuegoAceptadaConCdr()
    {
        var ticket = await _sunat.EnviarAsync(EmpaquetadorGre.Empaquetar(XmlFirmado()).ComoSolicitud());

        var primera = await _sunat.ConsultarAsync(ticket.NumTicket);
        var segunda = await _sunat.ConsultarAsync(ticket.NumTicket);

        Assert.Equal(EstadoEnvio.EnProceso, primera.CodRespuesta);
        Assert.Null(primera.Cdr);
        Assert.Equal(EstadoEnvio.Terminado, segunda.CodRespuesta);
        var cdr = LectorCdr.Leer(segunda.Cdr!);
        Assert.Equal(EstadoCdr.Aceptada, cdr.Estado);
        Assert.Equal("0", cdr.Codigo);
        Assert.Equal("T001-1", cdr.IdDocumento);
        Assert.Contains("ha sido aceptada", cdr.Descripcion);
    }

    [Fact]
    public async Task EsperarAsync_ConsultaHastaTenerResultado()
    {
        var sunat = new EnvioSunatSimulado(_certificado, TimeProvider.System, consultasEnProceso: 3);
        var ticket = await sunat.EnviarAsync(EmpaquetadorGre.Empaquetar(XmlFirmado()).ComoSolicitud());

        var estado = await sunat.EsperarAsync(ticket.NumTicket, maxIntentos: 5, TimeSpan.Zero, TimeProvider.System);

        Assert.Equal(EstadoEnvio.Terminado, estado.CodRespuesta);
    }

    [Fact]
    public async Task EsperarAsync_SeAcabanLosIntentos_DevuelveEnProceso_SinReenviar()
    {
        var sunat = new EnvioSunatSimulado(_certificado, TimeProvider.System, consultasEnProceso: 10);
        var ticket = await sunat.EnviarAsync(EmpaquetadorGre.Empaquetar(XmlFirmado()).ComoSolicitud());

        var estado = await sunat.EsperarAsync(ticket.NumTicket, maxIntentos: 2, TimeSpan.Zero, TimeProvider.System);

        Assert.True(estado.SigueEnProceso);
    }

    [Fact]
    public async Task PlacaNoRegistrada_AceptadaConObservacion4398()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado();
        guia = guia with { Transporte = guia.Transporte with { Vehiculos = ["ABC123", "ZZZ999"] } };

        var cdr = await EnviarYLeerCdr(XmlFirmado(guia));

        Assert.Equal(EstadoCdr.Observada, cdr.Estado);
        var observacion = Assert.Single(cdr.Observaciones);
        Assert.Equal("4398", observacion.Codigo);
        Assert.Contains("ZZZ999", observacion.Descripcion);
    }

    // ---------- Rechazos (con CDR) ----------

    [Fact]
    public async Task DatoCambiadoDespuesDeFirmar_Rechazo2335()
    {
        var alterado = XmlFirmado().Replace(">1250.5<", ">9250.5<");

        var cdr = await EnviarYLeerCdr(alterado);

        Assert.Equal(EstadoCdr.Rechazada, cdr.Estado);
        Assert.Equal("2335", cdr.Codigo);
    }

    [Fact]
    public async Task FirmadoConOtroCertificado_Rechazo2325()
    {
        using var otro = Certificados.Crear("CN=Certificado no comunicado");
        var xml = FirmadorXml.Firmar(GeneradorXml.GenerarTexto(GuiaEjemplo.VentaConTransportePrivado()), otro);

        var cdr = await EnviarYLeerCdr(xml);

        Assert.Equal(EstadoCdr.Rechazada, cdr.Estado);
        Assert.Equal("2325", cdr.Codigo);
    }

    // ---------- Errores sin CDR (el archivo no llega a evaluarse) ----------

    [Fact]
    public async Task SinFirma_Error1059_SinCdr()
    {
        var estado = await EnviarYEsperar(EmpaquetadorGre.Empaquetar(GeneradorXml.GenerarTexto(GuiaEjemplo.VentaConTransportePrivado())));

        Assert.Equal(EstadoEnvio.ConError, estado.CodRespuesta);
        Assert.Null(estado.Cdr);
        Assert.Equal("1059", estado.Error!.NumError);
    }

    [Theory]
    [InlineData("20100000068-09-T001-1", "1034")] // otro RUC
    [InlineData("20100000050-09-T002-1", "1035")] // otra serie
    [InlineData("20100000050-09-T001-2", "1036")] // otro número
    public async Task NombreNoCoincideConElContenido_Error(string nombreBase, string codigo)
    {
        var estado = await EnviarYEsperar(EmpaquetadorGre.Empaquetar(nombreBase, XmlFirmado()));

        Assert.Equal(codigo, estado.Error!.NumError);
    }

    [Fact]
    public async Task XmlQueNoCumpleElEsquema_Error0305()
    {
        var xml = XmlFirmado().Replace("<cbc:IssueDate>2026-10-09</cbc:IssueDate>", "<cbc:IssueDate>09/10/2026</cbc:IssueDate>");

        var estado = await EnviarYEsperar(EmpaquetadorGre.Empaquetar(NombreBase, xml));

        Assert.Equal("0305", estado.Error!.NumError);
    }

    [Fact]
    public async Task XmlMalFormado_Error0306()
    {
        var estado = await EnviarYEsperar(EmpaquetadorGre.Empaquetar(NombreBase, "<DespatchAdvice>"));

        Assert.Equal("0306", estado.Error!.NumError);
    }

    // ---------- Rechazos al enviar (sin ticket) ----------

    [Fact]
    public async Task HashQueNoCoincide_SeRechazaAlEnviar()
    {
        var solicitud = EmpaquetadorGre.Empaquetar(XmlFirmado()).ComoSolicitud() with { HashZip = new string('0', 64) };

        var error = await Assert.ThrowsAsync<EnvioSunatException>(() => _sunat.EnviarAsync(solicitud));

        Assert.Equal("0156", error.Codigo);
    }

    [Theory]
    [InlineData("guia.zip")]
    [InlineData("20100000050-01-T001-1.zip")] // tipo 01 = factura
    [InlineData("20100000050-09-T001-1.xml")]
    public async Task NombreDeArchivoIncorrecto_0151(string nombre)
    {
        var solicitud = EmpaquetadorGre.Empaquetar(XmlFirmado()).ComoSolicitud() with { NomArchivo = nombre };

        var error = await Assert.ThrowsAsync<EnvioSunatException>(() => _sunat.EnviarAsync(solicitud));

        Assert.Equal("0151", error.Codigo);
    }

    [Fact]
    public async Task XmlConOtroNombreDentroDelZip_0161()
    {
        var zip = Zip(("otro-nombre.xml", XmlFirmado()));

        var error = await Assert.ThrowsAsync<EnvioSunatException>(() => _sunat.EnviarAsync(Solicitud(zip)));

        Assert.Equal("0161", error.Codigo);
    }

    [Fact]
    public async Task ZipConDosArchivos_0158()
    {
        var zip = Zip((NombreBase + ".xml", XmlFirmado()), ("extra.txt", "x"));

        var error = await Assert.ThrowsAsync<EnvioSunatException>(() => _sunat.EnviarAsync(Solicitud(zip)));

        Assert.Equal("0158", error.Codigo);
    }

    [Fact]
    public async Task ZipVacio_0155()
    {
        var error = await Assert.ThrowsAsync<EnvioSunatException>(() => _sunat.EnviarAsync(Solicitud(Zip())));

        Assert.Equal("0155", error.Codigo);
    }

    [Fact]
    public async Task NoEsUnZip_0156()
    {
        var error = await Assert.ThrowsAsync<EnvioSunatException>(() => _sunat.EnviarAsync(Solicitud("no soy un zip"u8.ToArray())));

        Assert.Equal("0156", error.Codigo);
    }

    [Fact]
    public async Task GuiaYaAceptada_ReenviarSeRechaza_2223()
    {
        var solicitud = EmpaquetadorGre.Empaquetar(XmlFirmado()).ComoSolicitud();
        await _sunat.EnviarAsync(solicitud);

        var error = await Assert.ThrowsAsync<EnvioSunatException>(() => _sunat.EnviarAsync(solicitud));

        Assert.Equal("2223", error.Codigo);
    }

    [Fact]
    public async Task GuiaRechazada_SePuedeCorregirYReenviar()
    {
        // Un rechazo no "gasta" el número: corregida, la misma guía se vuelve a enviar.
        await _sunat.EnviarAsync(EmpaquetadorGre.Empaquetar(XmlFirmado().Replace(">1250.5<", ">9250.5<")).ComoSolicitud());

        var ticket = await _sunat.EnviarAsync(EmpaquetadorGre.Empaquetar(XmlFirmado()).ComoSolicitud());

        Assert.NotNull(ticket);
    }

    [Fact]
    public async Task TicketDesconocido_0127()
    {
        var error = await Assert.ThrowsAsync<EnvioSunatException>(() => _sunat.ConsultarAsync("no-existe"));

        Assert.Equal("0127", error.Codigo);
    }

    // ---------- Lector del CDR ----------

    [Theory]
    [InlineData("0", 0, EstadoCdr.Aceptada)]
    [InlineData("0", 1, EstadoCdr.Observada)]
    [InlineData("4398", 0, EstadoCdr.Observada)]
    [InlineData("2335", 0, EstadoCdr.Rechazada)]
    [InlineData("3343", 0, EstadoCdr.Rechazada)]
    public void Cdr_EstadoSegunCodigoYObservaciones(string codigo, int observaciones, EstadoCdr esperado)
    {
        var resultado = new ResultadoCdr("T001-1", codigo, "", Enumerable.Repeat(new Observacion("4398", "x"), observaciones).ToList());

        Assert.Equal(esperado, resultado.Estado);
    }

    [Fact]
    public void Cdr_ZipSinRespuesta_SeRechaza()
    {
        Assert.Throws<InvalidDataException>(() => LectorCdr.Leer(Zip(("otro.xml", "<a/>"))));
    }

    public void Dispose() => _certificado.Dispose();

    private async Task<EstadoEnvio> EnviarYEsperar(PaqueteGre paquete)
    {
        var ticket = await _sunat.EnviarAsync(paquete.ComoSolicitud());
        return await _sunat.EsperarAsync(ticket.NumTicket, maxIntentos: 3, TimeSpan.Zero, TimeProvider.System);
    }

    private async Task<ResultadoCdr> EnviarYLeerCdr(string xmlFirmado)
    {
        var estado = await EnviarYEsperar(EmpaquetadorGre.Empaquetar(NombreBase, xmlFirmado));
        return LectorCdr.Leer(estado.Cdr ?? throw new Xunit.Sdk.XunitException($"Sin CDR: {estado.Error}"));
    }

    private static SolicitudEnvio Solicitud(byte[] zip) =>
        new(NombreBase + ".zip", Convert.ToBase64String(zip), Convert.ToHexStringLower(SHA256.HashData(zip)));

    private static byte[] Zip(params (string Nombre, string Contenido)[] archivos)
    {
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (nombre, contenido) in archivos)
            {
                using var escritor = new StreamWriter(zip.CreateEntry(nombre).Open(), new UTF8Encoding(false));
                escritor.Write(contenido);
            }
        }
        return memoria.ToArray();
    }
}
