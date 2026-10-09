using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GreXml.Tests;

/// <summary>Certificados autofirmados creados en memoria: el repositorio no guarda ningún .pfx ni contraseña.</summary>
internal static class Certificados
{
    public static X509Certificate2 Crear(string sujeto)
    {
        using var rsa = RSA.Create(2048);
        var solicitud = new CertificateRequest(sujeto, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var hoy = DateTimeOffset.UtcNow;
        return solicitud.CreateSelfSigned(hoy.AddDays(-1), hoy.AddYears(1));
    }
}
