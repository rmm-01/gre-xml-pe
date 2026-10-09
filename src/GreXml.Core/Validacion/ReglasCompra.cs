using GreXml.Core.Modelo;

namespace GreXml.Core.Validacion;

/// <summary>
/// Motivo 02: el remitente compró los bienes y los lleva a su propio local. Por eso el destinatario es el mismo
/// remitente, y quien vendió aparece como proveedor.
/// </summary>
public sealed class ReglasCompra : IReglasMotivo
{
    public string Motivo => MotivosTraslado.Compra;

    public bool LlevaProveedor => true;

    public IEnumerable<ErrorValidacion> Validar(GuiaRemision guia)
    {
        if (!Participantes.DestinatarioEsRemitente(guia))
            yield return ErrorValidacion.De(CatalogoReglas.DestinatarioDebeSerRemitente, "destinatario.numeroDocumento",
                "En una compra el destinatario es el mismo remitente (con su RUC).");

        // Nadie se compra a sí mismo: el proveedor es otra empresa.
        if (guia.Proveedor is { } proveedor && proveedor.Ruc == guia.Remitente.Ruc)
            yield return ErrorValidacion.De(CatalogoReglas.ProveedorIgualARemitente, "proveedor.ruc",
                "El proveedor no puede ser el mismo remitente.");
    }
}
