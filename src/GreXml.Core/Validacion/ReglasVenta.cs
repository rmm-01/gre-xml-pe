using GreXml.Core.Modelo;

namespace GreXml.Core.Validacion;

/// <summary>Motivo 01: el remitente vendió los bienes y los traslada al comprador.</summary>
public sealed class ReglasVenta : IReglasMotivo
{
    public string Motivo => MotivosTraslado.Venta;

    public bool LlevaProveedor => false;

    public IEnumerable<ErrorValidacion> Validar(GuiaRemision guia)
    {
        // En una venta, comprador y vendedor son personas distintas.
        if (Participantes.DestinatarioEsRemitente(guia))
            yield return ErrorValidacion.De(CatalogoReglas.VentaDestinatarioIgualARemitente, "destinatario.numeroDocumento",
                "En una venta el destinatario no puede ser el mismo remitente.");
    }
}
