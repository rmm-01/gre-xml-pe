using GreXml.Core.Envio;
using GreXml.Core.Validacion;
using GreXml.Core.Xml;

namespace GreXml.Api;

/// <summary>Resultado de /guias/validar. Errores vacíos = la guía cumple todas las reglas.</summary>
public sealed record RespuestaValidacion(bool Valida, IReadOnlyList<ErrorValidacion> Errores);

/// <summary>
/// Resultado de /guias/generar: el XML firmado, lo que se comprobó sobre él y el ZIP listo para enviar.
/// </summary>
public sealed record RespuestaGeneracion(
    string NombreArchivo,
    string XmlFirmado,
    bool CumpleEsquema,
    IReadOnlyList<ErrorXsd> ErroresEsquema,
    bool FirmaValida,
    string ZipBase64,
    string HashZip);

/// <summary>
/// Resultado de un envío al simulador. Si <see cref="CodRespuesta"/> es "98", la guía sigue en proceso y se
/// consulta después con GET /envios/{ticket}.
/// </summary>
public sealed record RespuestaEnvio(string Ticket, string CodRespuesta, RespuestaCdr? Cdr, ErrorSunat? Error)
{
    public static RespuestaEnvio De(string ticket, EstadoEnvio estado) =>
        new(ticket, estado.CodRespuesta, estado.Cdr is null ? null : RespuestaCdr.De(LectorCdr.Leer(estado.Cdr)), estado.Error);
}

public sealed record RespuestaCdr(EstadoCdr Estado, string Codigo, string Descripcion, IReadOnlyList<Observacion> Observaciones)
{
    public static RespuestaCdr De(ResultadoCdr cdr) => new(cdr.Estado, cdr.Codigo, cdr.Descripcion, cdr.Observaciones);
}
