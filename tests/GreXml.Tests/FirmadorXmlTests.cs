using System.Security.Cryptography.X509Certificates;
using System.Xml;
using GreXml.Core.Xml;

namespace GreXml.Tests;

/// <summary>Cada ejecución crea sus propios certificados (ver <see cref="Certificados"/>).</summary>
public sealed class FirmadorXmlTests : IDisposable
{
    private const string Ds = "http://www.w3.org/2000/09/xmldsig#";

    private readonly X509Certificate2 _certificado = Certificados.Crear("CN=Remitente de prueba");
    private readonly X509Certificate2 _otroCertificado = Certificados.Crear("CN=Otra empresa");

    private static string XmlSinFirmar() => GeneradorXml.GenerarTexto(GuiaEjemplo.VentaConTransportePrivado());

    [Fact]
    public void Firmado_SeVerificaConElMismoCertificado()
    {
        var firmado = FirmadorXml.Firmar(XmlSinFirmar(), _certificado);

        Assert.True(FirmadorXml.Verificar(firmado, _certificado));
    }

    [Fact]
    public void Firmado_SigueCumpliendoElEsquemaUbl()
    {
        var firmado = FirmadorXml.Firmar(XmlSinFirmar(), _certificado);

        Assert.Empty(ValidadorXsd.Validar(firmado));
    }

    [Fact]
    public void Firmado_ConOtroCertificado_NoSeVerifica()
    {
        var firmado = FirmadorXml.Firmar(XmlSinFirmar(), _otroCertificado);

        Assert.False(FirmadorXml.Verificar(firmado, _certificado));
    }

    [Fact]
    public void CambiarUnDatoDespuesDeFirmar_InvalidaLaFirma()
    {
        var firmado = FirmadorXml.Firmar(XmlSinFirmar(), _certificado);
        // Un solo dígito del peso: 1250.5 kg pasa a 9250.5 kg.
        var alterado = firmado.Replace(">1250.5<", ">9250.5<");

        Assert.NotEqual(firmado, alterado);
        Assert.False(FirmadorXml.Verificar(alterado, _certificado));
    }

    [Fact]
    public void ReformatearDespuesDeFirmar_InvalidaLaFirma()
    {
        // Reindentar no cambia los datos, pero sí los bytes firmados: por eso el XML firmado no se reformatea.
        var firmado = FirmadorXml.Firmar(XmlSinFirmar(), _certificado);
        var reformateado = firmado.Replace("<cbc:UBLVersionID>", "\n    <cbc:UBLVersionID>");

        Assert.False(FirmadorXml.Verificar(reformateado, _certificado));
    }

    [Fact]
    public void Estructura_FirmaEnUblExtensions_PrimerHijoDelDocumento()
    {
        var documento = Cargar(FirmadorXml.Firmar(XmlSinFirmar(), _certificado));
        var primerHijo = documento.DocumentElement!.FirstChild!;

        Assert.Equal("UBLExtensions", primerHijo.LocalName);
        var firma = (XmlElement)documento.GetElementsByTagName("Signature", Ds)[0]!;
        Assert.Equal("ExtensionContent", firma.ParentNode!.LocalName);
        Assert.Equal(FirmadorXml.IdFirma, firma.GetAttribute("Id"));
    }

    [Fact]
    public void Estructura_AlgoritmosRsaSha256YEnveloped()
    {
        var documento = Cargar(FirmadorXml.Firmar(XmlSinFirmar(), _certificado));

        Assert.Equal("http://www.w3.org/2001/04/xmldsig-more#rsa-sha256", Algoritmo(documento, "SignatureMethod"));
        Assert.Equal("http://www.w3.org/2001/04/xmlenc#sha256", Algoritmo(documento, "DigestMethod"));
        Assert.Equal("http://www.w3.org/2000/09/xmldsig#enveloped-signature", Algoritmo(documento, "Transform"));
        Assert.Equal("", ((XmlElement)documento.GetElementsByTagName("Reference", Ds)[0]!).GetAttribute("URI"));
    }

    [Fact]
    public void Cabecera_CacSignature_ConDatosDelRemitenteYReferenciaALaFirma()
    {
        var documento = Cargar(FirmadorXml.Firmar(XmlSinFirmar(), _certificado));
        var ns = new XmlNamespaceManager(documento.NameTable);
        ns.AddNamespace("cac", GeneradorXml.Cac.NamespaceName);
        ns.AddNamespace("cbc", GeneradorXml.Cbc.NamespaceName);
        var firma = documento.DocumentElement!.SelectSingleNode("cac:Signature", ns)!;

        Assert.Equal(GuiaEjemplo.RucRemitente, firma.SelectSingleNode("cac:SignatoryParty/cac:PartyIdentification/cbc:ID", ns)!.InnerText);
        Assert.Equal("DISTRIBUIDORA EJEMPLO SAC", firma.SelectSingleNode("cac:SignatoryParty/cac:PartyName/cbc:Name", ns)!.InnerText);
        Assert.Equal("#FirmaGRE", firma.SelectSingleNode("cac:DigitalSignatureAttachment/cac:ExternalReference/cbc:URI", ns)!.InnerText);
        Assert.Equal("DespatchSupplierParty", firma.NextSibling!.LocalName);
    }

    [Fact]
    public void ObtenerCertificado_DevuelveElQueFirmo_SinClavePrivada()
    {
        var firmado = FirmadorXml.Firmar(XmlSinFirmar(), _certificado);

        using var incluido = FirmadorXml.ObtenerCertificado(firmado);

        Assert.Equal(_certificado.Thumbprint, incluido!.Thumbprint);
        Assert.False(incluido.HasPrivateKey); // el XML solo lleva la parte pública
        Assert.Null(FirmadorXml.ObtenerCertificado(XmlSinFirmar()));
    }

    [Fact]
    public void FirmarDosVeces_SeRechaza()
    {
        var firmado = FirmadorXml.Firmar(XmlSinFirmar(), _certificado);

        Assert.Throws<InvalidOperationException>(() => FirmadorXml.Firmar(firmado, _certificado));
    }

    [Fact]
    public void CertificadoSinClavePrivada_SeRechaza()
    {
        using var soloPublico = X509CertificateLoader.LoadCertificate(_certificado.Export(X509ContentType.Cert));

        Assert.Throws<ArgumentException>(() => FirmadorXml.Firmar(XmlSinFirmar(), soloPublico));
    }

    [Fact]
    public void Verificar_XmlSinFirma_EsFalso()
    {
        Assert.False(FirmadorXml.Verificar(XmlSinFirmar(), _certificado));
    }

    [Fact]
    public void Verificar_XmlMalFormado_EsFalso()
    {
        Assert.False(FirmadorXml.Verificar("<DespatchAdvice>", _certificado));
    }

    public void Dispose()
    {
        _certificado.Dispose();
        _otroCertificado.Dispose();
    }

    private static string Algoritmo(XmlDocument documento, string elemento) =>
        ((XmlElement)documento.GetElementsByTagName(elemento, Ds)[0]!).GetAttribute("Algorithm");

    private static XmlDocument Cargar(string xml)
    {
        var documento = new XmlDocument { PreserveWhitespace = true };
        documento.LoadXml(xml);
        return documento;
    }
}
