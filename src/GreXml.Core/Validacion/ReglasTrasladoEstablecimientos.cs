using GreXml.Core.Modelo;

namespace GreXml.Core.Validacion;

/// <summary>Motivo 04: la empresa mueve sus propios bienes de un local suyo a otro. No hay venta ni proveedor.</summary>
public sealed class ReglasTrasladoEstablecimientos : IReglasMotivo
{
    public string Motivo => MotivosTraslado.TrasladoEntreEstablecimientos;

    public bool LlevaProveedor => false;

    public IEnumerable<ErrorValidacion> Validar(GuiaRemision guia)
    {
        if (!Participantes.DestinatarioEsRemitente(guia))
            yield return ErrorValidacion.De(CatalogoReglas.DestinatarioDebeSerRemitente, "destinatario.numeroDocumento",
                "En un traslado entre establecimientos el destinatario es el mismo remitente (con su RUC).");

        // Regla propia, sin código SUNAT: salir y llegar al mismo lugar no es un traslado entre establecimientos.
        if (MismoLugar(guia.Partida, guia.Llegada))
            yield return ErrorValidacion.De(CatalogoReglas.PartidaIgualALlegada, "llegada.direccion",
                "El punto de llegada debe ser un establecimiento distinto al de partida.");
    }

    private static bool MismoLugar(PuntoTraslado a, PuntoTraslado b) =>
        a.Ubigeo == b.Ubigeo && string.Equals(Normalizar(a.Direccion), Normalizar(b.Direccion), StringComparison.OrdinalIgnoreCase);

    // "AV. LOS EJEMPLOS  123" y "av. los ejemplos 123" son la misma dirección.
    private static string Normalizar(string? direccion) =>
        string.Join(' ', (direccion ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
