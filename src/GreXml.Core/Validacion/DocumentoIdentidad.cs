namespace GreXml.Core.Validacion;

/// <summary>Validación local de documentos de identidad: no consulta ningún servicio.</summary>
public static class DocumentoIdentidad
{
    // 10 = persona natural con DNI, 15/16/17 = otras personas naturales, 20 = persona jurídica.
    private static readonly string[] PrefijosRuc = ["10", "15", "16", "17", "20"];

    // Pesos que SUNAT aplica a los 10 primeros dígitos del RUC para calcular el verificador (módulo 11).
    private static readonly int[] PesosRuc = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    public static bool EsRucValido(string? ruc)
    {
        if (!SoloDigitos(ruc, 11) || !PrefijosRuc.Contains(ruc![..2]))
            return false;

        var suma = 0;
        for (var i = 0; i < PesosRuc.Length; i++)
            suma += (ruc[i] - '0') * PesosRuc[i];

        var digito = (11 - suma % 11) switch
        {
            10 => 0,
            11 => 1,
            var d => d,
        };
        return ruc[^1] - '0' == digito;
    }

    public static bool EsDniValido(string? dni) => SoloDigitos(dni, 8);

    /// <summary>Carné de extranjería y pasaporte: letras y números, hasta 15 caracteres.</summary>
    public static bool EsAlfanumericoValido(string? numero) =>
        !string.IsNullOrEmpty(numero) && numero.Length <= 15 && numero.All(char.IsAsciiLetterOrDigit);

    // IsAsciiDigit y no IsDigit: IsDigit también acepta dígitos de otros alfabetos (por ejemplo, '٣').
    private static bool SoloDigitos(string? numero, int longitud) =>
        numero is not null && numero.Length == longitud && numero.All(char.IsAsciiDigit);
}
