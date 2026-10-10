using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using GreXml.Api;
using GreXml.Core.Envio;
using GreXml.Core.Modelo;
using GreXml.Core.Validacion;
using GreXml.Core.Xml;
using Microsoft.AspNetCore.Http.HttpResults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(json =>
{
    // "Privado" en lugar de 1; y un null o un dato obligatorio ausente se rechaza con 400 en lugar de llegar al validador.
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    json.SerializerOptions.RespectNullableAnnotations = true;
    json.SerializerOptions.RespectRequiredConstructorParameters = true;
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(GuiaValidador.ConMotivosSoportados());
builder.Services.AddSingleton(_ => CertificadoDemo.Crear());
// El simulador recuerda tickets y guías aceptadas: una sola instancia para toda la API.
builder.Services.AddSingleton<IEnvioSunat>(sp =>
    new EnvioSunatSimulado(sp.GetRequiredService<X509Certificate2>(), sp.GetRequiredService<TimeProvider>()));

var app = builder.Build();

// La documentación interactiva solo se expone en desarrollo, no en producción.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(opciones => opciones.WithTitle("API de Guía de Remisión Electrónica (GRE)"));
    app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();
}

app.UseHttpsRedirection();

var guias = app.MapGroup("/guias").WithTags("Guías");

guias.MapGet("/ejemplo", Results<Ok<GuiaRemision>, ValidationProblem> (
    [Description("Motivo de traslado: 01 venta, 02 compra, 04 traslado entre establecimientos.")] string? motivo,
    TimeProvider reloj) =>
{
    motivo ??= MotivosTraslado.Venta;
    if (motivo is not (MotivosTraslado.Venta or MotivosTraslado.Compra or MotivosTraslado.TrasladoEntreEstablecimientos))
        return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["motivo"] = ["Use 01, 02 o 04."] });

    return TypedResults.Ok(GuiasDeEjemplo.Para(motivo, HoyEnPeru(reloj)));
})
.WithName("ObtenerGuiaDeEjemplo")
.WithSummary("Guía de ejemplo con datos inventados")
.WithDescription("Devuelve una guía válida para el motivo indicado, lista para enviar a /guias/validar, /generar o /enviar.");

guias.MapPost("/validar", (GuiaRemision guia, GuiaValidador validador) =>
{
    var errores = validador.Validar(guia);
    return TypedResults.Ok(new RespuestaValidacion(errores.Count == 0, errores));
})
.WithName("ValidarGuia")
.WithSummary("Revisa la guía contra las reglas de SUNAT")
.WithDescription(
    "Devuelve todos los errores juntos. Cada uno trae su código propio (GRE-xxx), el código SUNAT equivalente " +
    "si existe, y la ruta del campo (por ejemplo, 'bienes[1].cantidad').");

guias.MapPost("/generar", Results<Ok<RespuestaGeneracion>, UnprocessableEntity<RespuestaValidacion>> (
    GuiaRemision guia, GuiaValidador validador, X509Certificate2 certificado) =>
{
    var errores = validador.Validar(guia);
    if (errores.Count > 0)
        return TypedResults.UnprocessableEntity(new RespuestaValidacion(false, errores));

    var xml = FirmadorXml.Firmar(GeneradorXml.GenerarTexto(guia), certificado);
    var erroresEsquema = ValidadorXsd.Validar(xml);
    var paquete = EmpaquetadorGre.Empaquetar(xml);
    var solicitud = paquete.ComoSolicitud();

    return TypedResults.Ok(new RespuestaGeneracion(
        paquete.NombreArchivo, xml, erroresEsquema.Count == 0, erroresEsquema,
        FirmadorXml.Verificar(xml, certificado), solicitud.ArcGreZip, solicitud.HashZip));
})
.WithName("GenerarGuia")
.WithSummary("Genera el XML UBL 2.1 firmado y el ZIP")
.WithDescription(
    "Valida la guía; si tiene errores responde 422 con la lista. Si no, genera el XML, lo firma con un certificado " +
    "de demostración, lo valida contra el XSD y devuelve el ZIP en Base64 con su hash SHA-256.");

guias.MapPost("/enviar", async Task<Results<Ok<RespuestaEnvio>, UnprocessableEntity<RespuestaValidacion>, ProblemHttpResult>> (
    GuiaRemision guia, GuiaValidador validador, X509Certificate2 certificado, IEnvioSunat sunat,
    TimeProvider reloj, CancellationToken ct) =>
{
    var errores = validador.Validar(guia);
    if (errores.Count > 0)
        return TypedResults.UnprocessableEntity(new RespuestaValidacion(false, errores));

    var paquete = EmpaquetadorGre.Empaquetar(FirmadorXml.Firmar(GeneradorXml.GenerarTexto(guia), certificado));
    Ticket ticket;
    try
    {
        ticket = await sunat.EnviarAsync(paquete.ComoSolicitud(), ct);
    }
    catch (EnvioSunatException ex)
    {
        // Rechazo antes de dar ticket: por ejemplo, la misma guía ya fue aceptada (2223).
        return TypedResults.Problem(title: "SUNAT (simulado) no aceptó el archivo", detail: ex.Message,
            statusCode: StatusCodes.Status422UnprocessableEntity, extensions: new Dictionary<string, object?> { ["codigo"] = ex.Codigo });
    }

    // Unas pocas consultas: si sigue en proceso, se devuelve el ticket para consultar después.
    var estado = await sunat.EsperarAsync(ticket.NumTicket, maxIntentos: 3, TimeSpan.FromMilliseconds(200), reloj, ct);
    return TypedResults.Ok(RespuestaEnvio.De(ticket.NumTicket, estado));
})
.WithName("EnviarGuia")
.WithSummary("Firma la guía y la envía a SUNAT simulado")
.WithDescription(
    "Nunca se conecta a SUNAT. El simulador revisa el archivo como lo haría SUNAT y responde con ticket y CDR: " +
    "Aceptada, Observada (placa que empieza con ZZZ) o Rechazada. Reenviar una guía aceptada responde 422 (código 2223).");

app.MapGet("/envios/{ticket}", async Task<Results<Ok<RespuestaEnvio>, NotFound>> (string ticket, IEnvioSunat sunat, CancellationToken ct) =>
{
    try
    {
        return TypedResults.Ok(RespuestaEnvio.De(ticket, await sunat.ConsultarAsync(ticket, ct)));
    }
    catch (EnvioSunatException ex) when (ex.Codigo == "0127")
    {
        return TypedResults.NotFound();
    }
})
.WithTags("Envíos")
.WithName("ConsultarEnvio")
.WithSummary("Estado de un envío por su ticket");

app.Run();

// Fecha de Perú (UTC-5), no la del servidor: a las 02:00 UTC todavía es el día anterior en Lima.
static DateOnly HoyEnPeru(TimeProvider reloj) => DateOnly.FromDateTime(reloj.GetUtcNow().ToOffset(TimeSpan.FromHours(-5)).DateTime);

// Permite que las pruebas de integración levanten la API con WebApplicationFactory<Program>.
public partial class Program;
