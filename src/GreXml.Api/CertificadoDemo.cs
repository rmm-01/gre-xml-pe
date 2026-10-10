using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GreXml.Api;

/// <summary>
/// Certificado autofirmado creado en memoria al iniciar la API: firma de verdad, pero ninguna entidad lo respalda,
/// así que SUNAT no lo aceptaría. No se guarda en disco ni en el repositorio.
/// </summary>
public static class CertificadoDemo
{
    public static X509Certificate2 Crear()
    {
        using var rsa = RSA.Create(2048);
        var solicitud = new CertificateRequest(
            "CN=DISTRIBUIDORA EJEMPLO SAC (certificado de demostracion), O=GreXmlPe",
            rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var hoy = DateTimeOffset.UtcNow;
        return solicitud.CreateSelfSigned(hoy.AddDays(-1), hoy.AddYears(1));
    }
}
