namespace GreXml.Core.Envio;

/// <summary>
/// Envío de la guía a SUNAT en dos pasos: enviar devuelve un ticket y, más tarde, el ticket se consulta hasta
/// obtener el CDR. Este proyecto solo incluye una implementación simulada (<see cref="EnvioSunatSimulado"/>).
/// </summary>
public interface IEnvioSunat
{
    /// <summary>Lanza <see cref="EnvioSunatException"/> si SUNAT no acepta el archivo para procesarlo.</summary>
    Task<Ticket> EnviarAsync(SolicitudEnvio solicitud, CancellationToken cancellationToken = default);

    Task<EstadoEnvio> ConsultarAsync(string numTicket, CancellationToken cancellationToken = default);
}

public sealed record Ticket(string NumTicket, DateTimeOffset FechaRecepcion);

/// <summary>
/// Respuesta de la consulta del ticket. <see cref="CodRespuesta"/>: "98" en proceso, "0" terminado con CDR,
/// "99" terminado con error (con CDR si fue rechazo; sin CDR si el archivo ni siquiera se pudo procesar).
/// </summary>
public sealed record EstadoEnvio(string CodRespuesta, byte[]? Cdr = null, ErrorSunat? Error = null)
{
    public const string EnProceso = "98";
    public const string Terminado = "0";
    public const string ConError = "99";

    public bool SigueEnProceso => CodRespuesta == EnProceso;
}

public sealed record ErrorSunat(string NumError, string DesError);

/// <summary>SUNAT rechazó la solicitud antes de darle un ticket (archivo dañado, nombre incorrecto, duplicado...).</summary>
public sealed class EnvioSunatException(string codigo, string mensaje) : Exception($"{codigo} - {mensaje}")
{
    public string Codigo { get; } = codigo;
}
