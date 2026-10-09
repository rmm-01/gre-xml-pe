namespace GreXml.Core.Envio;

public static class EsperaCdr
{
    /// <summary>
    /// Consulta el ticket hasta que deje de estar "en proceso" o se acaben los intentos. Si se acaban, devuelve el
    /// estado "98": la guía no se reenvía (eso la duplicaría); se guarda el ticket y se consulta más tarde.
    /// </summary>
    public static async Task<EstadoEnvio> EsperarAsync(this IEnvioSunat envio, string numTicket, int maxIntentos,
        TimeSpan intervalo, TimeProvider reloj, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxIntentos, 1);

        for (var intento = 1; ; intento++)
        {
            var estado = await envio.ConsultarAsync(numTicket, cancellationToken);
            if (!estado.SigueEnProceso || intento == maxIntentos)
                return estado;

            await Task.Delay(intervalo, reloj, cancellationToken);
        }
    }
}
