namespace GreXml.Core.Validacion;

/// <summary>
/// Una regla incumplida. <paramref name="Campo"/> es la ruta del dato en el JSON de la guía
/// (por ejemplo, "bienes[2].cantidad"), para que una pantalla pueda mostrar el error junto al campo.
/// </summary>
public sealed record ErrorValidacion(string Codigo, string? CodigoSunat, string Campo, string Mensaje)
{
    public static ErrorValidacion De(Regla regla, string campo, string mensaje) =>
        new(regla.Codigo, regla.CodigoSunat, campo, mensaje);
}
