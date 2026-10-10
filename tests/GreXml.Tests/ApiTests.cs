using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GreXml.Api;
using GreXml.Core.Envio;
using GreXml.Core.Modelo;
using GreXml.Core.Validacion;
using GreXml.Core.Xml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GreXml.Tests;

/// <summary>Levanta la API en memoria y la usa como lo haría la pantalla: JSON de entrada y de salida.</summary>
public sealed class ApiTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _cliente = fabrica.CreateClient();

    // El simulador vive mientras viva la API y recuerda las guías aceptadas: cada envío usa su propio número.
    private static int _ultimoNumero = 1000;

    private static GuiaRemision ConNumeroNuevo(GuiaRemision guia) => guia with { Numero = Interlocked.Increment(ref _ultimoNumero) };

    [Theory]
    [InlineData("01")]
    [InlineData("02")]
    [InlineData("04")]
    public async Task Ejemplo_DeCadaMotivo_EsValido(string motivo)
    {
        var guia = await _cliente.GetFromJsonAsync<GuiaRemision>($"/guias/ejemplo?motivo={motivo}", Json);

        Assert.Equal(motivo, guia!.MotivoTraslado);
        var validacion = await Leer<RespuestaValidacion>(await Post("/guias/validar", guia));
        Assert.True(validacion.Valida);
    }

    [Fact]
    public async Task Ejemplo_MotivoDesconocido_400()
    {
        var respuesta = await _cliente.GetAsync("/guias/ejemplo?motivo=99");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Ejemplo_EnumComoTexto()
    {
        var json = await _cliente.GetStringAsync("/guias/ejemplo");

        Assert.Contains("\"modalidad\":\"Privado\"", json);
    }

    [Fact]
    public async Task Validar_ConErrores_200ConCodigosYCampos()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with { PesoBrutoTotal = 0, Observaciones = new string('x', 251) };

        var respuesta = await Post("/guias/validar", guia);
        var validacion = await Leer<RespuestaValidacion>(respuesta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.False(validacion.Valida);
        Assert.Equal(
            [("observaciones", "GRE-109", "4186"), ("pesoBrutoTotal", "GRE-202", null)],
            validacion.Errores.Select(e => (e.Campo, e.Codigo, e.CodigoSunat)));
    }

    [Fact]
    public async Task Generar_GuiaValida_XmlFirmadoQueCumpleElEsquema()
    {
        var respuesta = await Post("/guias/generar", GuiaEjemplo.VentaConTransportePrivado());
        var generada = await Leer<RespuestaGeneracion>(respuesta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("20100000050-09-T001-1.zip", generada.NombreArchivo);
        Assert.True(generada.CumpleEsquema);
        Assert.True(generada.FirmaValida);
        Assert.Empty(ValidadorXsd.Validar(generada.XmlFirmado));
        Assert.Matches("^[0-9a-f]{64}$", generada.HashZip);
    }

    [Fact]
    public async Task Generar_ElZipContieneElMismoXml()
    {
        var generada = await Leer<RespuestaGeneracion>(await Post("/guias/generar", GuiaEjemplo.VentaConTransportePrivado()));

        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(Convert.FromBase64String(generada.ZipBase64)));
        using var lector = new StreamReader(Assert.Single(zip.Entries).Open());

        Assert.Equal(generada.XmlFirmado, lector.ReadToEnd());
    }

    [Fact]
    public async Task Generar_GuiaConErrores_422ConLaLista()
    {
        var respuesta = await Post("/guias/generar", GuiaEjemplo.VentaConTransportePrivado() with { Serie = "F001" });
        var validacion = await Leer<RespuestaValidacion>(respuesta);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal("serie", Assert.Single(validacion.Errores).Campo);
    }

    [Fact]
    public async Task Enviar_GuiaValida_AceptadaConCdr()
    {
        var respuesta = await Post("/guias/enviar", ConNumeroNuevo(GuiaEjemplo.VentaConTransportePrivado()));
        var envio = await Leer<RespuestaEnvio>(respuesta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(EstadoEnvio.Terminado, envio.CodRespuesta);
        Assert.Equal(EstadoCdr.Aceptada, envio.Cdr!.Estado);
        Assert.Null(envio.Error);
    }

    [Fact]
    public async Task Enviar_PlacaNoRegistrada_Observada()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado();
        guia = ConNumeroNuevo(guia with { Transporte = guia.Transporte with { Vehiculos = ["ZZZ999"] } });

        var envio = await Leer<RespuestaEnvio>(await Post("/guias/enviar", guia));

        Assert.Equal(EstadoCdr.Observada, envio.Cdr!.Estado);
        Assert.Equal("4398", Assert.Single(envio.Cdr.Observaciones).Codigo);
    }

    [Fact]
    public async Task Enviar_LaMismaGuiaDosVeces_SegundaVez422Con2223()
    {
        var guia = ConNumeroNuevo(GuiaEjemplo.VentaConTransportePrivado());
        await Post("/guias/enviar", guia);

        var respuesta = await Post("/guias/enviar", guia);
        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal("2223", problema!.Extensions["codigo"]!.ToString());
    }

    [Fact]
    public async Task Envio_ConsultarTicket_DevuelveElMismoResultado()
    {
        var envio = await Leer<RespuestaEnvio>(await Post("/guias/enviar", ConNumeroNuevo(GuiaEjemplo.VentaConTransportePrivado())));

        var consulta = await _cliente.GetFromJsonAsync<RespuestaEnvio>($"/envios/{envio.Ticket}", Json);

        Assert.Equal(EstadoCdr.Aceptada, consulta!.Cdr!.Estado);
    }

    [Fact]
    public async Task Envio_TicketDesconocido_404()
    {
        var respuesta = await _cliente.GetAsync("/envios/no-existe");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("""{"serie":"T001"}""")] // faltan datos obligatorios
    [InlineData("""{"serie":"T001","numero":1,"fechaEmision":"2026-10-09","horaEmision":"09:00:00","remitente":null}""")]
    [InlineData("no es json")]
    public async Task Json_IncompletoONulo_400_NoLlegaAlValidador(string cuerpo)
    {
        var respuesta = await _cliente.PostAsync("/guias/validar", new StringContent(cuerpo, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Json_ConductorConCampoNulo_400()
    {
        var json = JsonSerializer.Serialize(GuiaEjemplo.VentaConTransportePrivado(), Json)
            .Replace("\"licencia\":\"Q12345678\"", "\"licencia\":null");

        var respuesta = await _cliente.PostAsync("/guias/validar", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    private Task<HttpResponseMessage> Post(string ruta, GuiaRemision guia) => _cliente.PostAsJsonAsync(ruta, guia, Json);

    private static async Task<T> Leer<T>(HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<T>(Json))!;
}
