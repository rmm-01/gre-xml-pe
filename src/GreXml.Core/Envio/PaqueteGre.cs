using System.Security.Cryptography;

namespace GreXml.Core.Envio;

/// <summary>ZIP listo para enviar: un solo XML firmado, con el nombre que exige SUNAT.</summary>
public sealed record PaqueteGre(string NombreBase, byte[] Zip)
{
    /// <summary>Por ejemplo, "20100000050-09-T001-1.zip".</summary>
    public string NombreArchivo => NombreBase + ".zip";

    /// <summary>SHA-256 del ZIP en hexadecimal: SUNAT lo recalcula para detectar un archivo dañado en el camino.</summary>
    public string HashZip => Convert.ToHexStringLower(SHA256.HashData(Zip));

    /// <summary>Cuerpo del envío, con los mismos campos que el JSON de la API de SUNAT.</summary>
    public SolicitudEnvio ComoSolicitud() => new(NombreArchivo, Convert.ToBase64String(Zip), HashZip);
}

/// <summary>
/// Lo que viaja a SUNAT: nomArchivo, arcGreZip (el ZIP en Base64, porque JSON solo transporta texto) y hashZip.
/// </summary>
public sealed record SolicitudEnvio(string NomArchivo, string ArcGreZip, string HashZip);
