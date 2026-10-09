namespace GreXml.Core.Validacion;

/// <summary>
/// Una regla de validación: código propio (estable, nunca se reutiliza) y, si existe, el código de error
/// con el que SUNAT rechaza el mismo caso.
/// </summary>
public sealed record Regla(string Codigo, string? CodigoSunat);

/// <summary>
/// Todas las reglas. Códigos propios agrupados por centena: 1xx datos generales, 2xx traslado y bienes,
/// 3xx transporte, 4xx reglas del motivo. Los códigos SUNAT vienen del catálogo de errores de la GRE;
/// null cuando SUNAT no tiene un código equivalente para el caso.
/// </summary>
public static class CatalogoReglas
{
    public static readonly Regla SerieInvalida = new("GRE-101", "1001");
    public static readonly Regla NumeroInvalido = new("GRE-102", "1001");
    public static readonly Regla RucRemitenteInvalido = new("GRE-103", null);
    public static readonly Regla RazonSocialRemitenteObligatoria = new("GRE-104", "1037");
    public static readonly Regla TipoDocumentoDestinatarioInvalido = new("GRE-105", "2760");
    public static readonly Regla DocumentoDestinatarioInvalido = new("GRE-106", "2758");
    public static readonly Regla NombreDestinatarioObligatorio = new("GRE-107", "2761");
    public static readonly Regla MotivoNoSoportado = new("GRE-108", null);
    public static readonly Regla ObservacionesDemasiadoLargas = new("GRE-109", "4186");

    public static readonly Regla FechaInicioAnteriorAEmision = new("GRE-201", "3343");
    public static readonly Regla PesoBrutoNoPositivo = new("GRE-202", null);
    public static readonly Regla UnidadPesoInvalida = new("GRE-203", "4154");
    public static readonly Regla UbigeoInvalido = new("GRE-204", "2776");
    public static readonly Regla DireccionObligatoria = new("GRE-205", "2777");
    public static readonly Regla SinBienes = new("GRE-206", "1064");
    public static readonly Regla DescripcionBienInvalida = new("GRE-207", "2782");
    public static readonly Regla CantidadBienNoPositiva = new("GRE-208", null);
    public static readonly Regla UnidadMedidaBienObligatoria = new("GRE-209", "2883");

    public static readonly Regla TransportistaObligatorio = new("GRE-301", "2534");
    public static readonly Regla RucTransportistaInvalido = new("GRE-302", "2559");
    public static readonly Regla TransportistaIgualARemitente = new("GRE-303", "2560");
    public static readonly Regla SinVehiculos = new("GRE-304", "1067");
    public static readonly Regla PlacaInvalida = new("GRE-305", "4167");
    public static readonly Regla SinConductores = new("GRE-306", "1068");
    public static readonly Regla DocumentoConductorInvalido = new("GRE-307", "2569");
    public static readonly Regla NombresConductorObligatorios = new("GRE-308", "3360");
    public static readonly Regla ApellidosConductorObligatorios = new("GRE-309", "3361");
    public static readonly Regla LicenciaConductorObligatoria = new("GRE-310", "2572");

    public static readonly Regla VentaDestinatarioIgualARemitente = new("GRE-401", "2555");
}
