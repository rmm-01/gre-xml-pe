namespace GreXml.Core.Modelo;

/// <summary>Catálogo 20 de SUNAT: motivo del traslado. Solo los que este proyecto soporta.</summary>
public static class MotivosTraslado
{
    public const string Venta = "01";
}

/// <summary>Catálogo 06 de SUNAT: tipo de documento de identidad. Solo los que este proyecto soporta.</summary>
public static class TiposDocumento
{
    public const string Dni = "1";
    public const string CarneExtranjeria = "4";
    public const string Ruc = "6";
    public const string Pasaporte = "7";
}

/// <summary>Unidad del peso bruto total: SUNAT solo acepta kilogramos (error 4154).</summary>
public static class UnidadesPeso
{
    public const string Kilogramos = "KGM";
}
