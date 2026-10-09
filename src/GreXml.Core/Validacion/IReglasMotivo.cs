using GreXml.Core.Modelo;

namespace GreXml.Core.Validacion;

/// <summary>
/// Reglas que solo aplican a un motivo de traslado. Agregar un motivo nuevo es agregar una implementación,
/// sin tocar las reglas generales.
/// </summary>
public interface IReglasMotivo
{
    /// <summary>Código del catálogo 20 (ver <see cref="MotivosTraslado"/>).</summary>
    string Motivo { get; }

    IEnumerable<ErrorValidacion> Validar(GuiaRemision guia);
}
