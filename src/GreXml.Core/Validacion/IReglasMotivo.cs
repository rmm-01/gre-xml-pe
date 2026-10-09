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

    /// <summary>true si el motivo exige informar al proveedor; si es false, la guía no debe traerlo.</summary>
    bool LlevaProveedor { get; }

    IEnumerable<ErrorValidacion> Validar(GuiaRemision guia);
}
