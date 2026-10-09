using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace GreXml.Core.Xml;

/// <summary>
/// Firma XMLDSig "enveloped" (la firma va dentro del mismo documento) con RSA-SHA256, como la pide SUNAT:
/// el bloque ds:Signature va en ext:UBLExtensions y la cabecera lleva una referencia a él en cac:Signature.
/// </summary>
public static class FirmadorXml
{
    public static readonly string Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    public const string IdFirma = "FirmaGRE";

    private static readonly string Cac = GeneradorXml.Cac.NamespaceName;
    private static readonly string Cbc = GeneradorXml.Cbc.NamespaceName;

    /// <summary>
    /// Devuelve el XML firmado. El certificado debe incluir la clave privada. Después de firmar, el texto no
    /// se puede reformatear: un espacio de más cambia el contenido y la firma deja de ser válida.
    /// </summary>
    public static string Firmar(string xml, X509Certificate2 certificado)
    {
        using var clave = certificado.GetRSAPrivateKey()
            ?? throw new ArgumentException("El certificado no tiene una clave privada RSA.", nameof(certificado));

        var documento = Cargar(xml);
        var raiz = documento.DocumentElement!;
        if (raiz.GetElementsByTagName("UBLExtensions", Ext).Count > 0)
            throw new InvalidOperationException("El XML ya está firmado.");

        // Contenedor de la firma: debe ser el primer hijo del documento, antes de cbc:UBLVersionID.
        var contenido = documento.CreateElement("ext", "ExtensionContent", Ext);
        var extensiones = documento.CreateElement("ext", "UBLExtensions", Ext);
        var extension = documento.CreateElement("ext", "UBLExtension", Ext);
        extension.AppendChild(contenido);
        extensiones.AppendChild(extension);
        raiz.PrependChild(extensiones);

        AgregarReferenciaDeFirma(documento);

        var firmado = new SignedXml(documento) { SigningKey = clave };
        firmado.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;
        firmado.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;
        firmado.Signature.Id = IdFirma;

        // URI "" = se firma el documento completo; el transform "enveloped" excluye a la propia firma del cálculo.
        var referencia = new Reference(string.Empty) { DigestMethod = SignedXml.XmlDsigSHA256Url };
        referencia.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        firmado.AddReference(referencia);

        var infoClave = new KeyInfo();
        infoClave.AddClause(new KeyInfoX509Data(certificado));
        firmado.KeyInfo = infoClave;

        firmado.ComputeSignature();
        contenido.AppendChild(documento.ImportNode(firmado.GetXml(), deep: true));

        return documento.OuterXml;
    }

    /// <summary>
    /// true si la firma es válida, cubre el documento completo y fue hecha con la clave de <paramref name="certificado"/>.
    /// No valida la cadena del certificado (un certificado de prueba autofirmado no la tiene).
    /// </summary>
    public static bool Verificar(string xml, X509Certificate2 certificado)
    {
        XmlDocument documento;
        try
        {
            documento = Cargar(xml);
        }
        catch (XmlException)
        {
            return false;
        }

        var firmas = documento.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl);
        if (firmas.Count != 1)
            return false;

        var firmado = new SignedXml(documento);
        firmado.LoadXml((XmlElement)firmas[0]!);

        // Una firma que solo cubre una parte permitiría cambiar el resto sin que se note.
        if (firmado.SignedInfo!.References.Count != 1 || ((Reference)firmado.SignedInfo.References[0]!).Uri != string.Empty)
            return false;

        return firmado.CheckSignature(certificado, verifySignatureOnly: true);
    }

    /// <summary>Certificado incluido en la firma (ds:X509Certificate), o null si el XML no está firmado.</summary>
    public static X509Certificate2? ObtenerCertificado(string xml)
    {
        var documento = Cargar(xml);
        var nodo = documento.GetElementsByTagName("X509Certificate", SignedXml.XmlDsigNamespaceUrl);
        return nodo.Count == 0 ? null : X509CertificateLoader.LoadCertificate(Convert.FromBase64String(nodo[0]!.InnerText));
    }

    /// <summary>
    /// cac:Signature en la cabecera: quién firma (los datos del remitente) y dónde está la firma (#FirmaGRE).
    /// El esquema UBL lo ubica justo antes de cac:DespatchSupplierParty.
    /// </summary>
    private static void AgregarReferenciaDeFirma(XmlDocument documento)
    {
        var espacios = new XmlNamespaceManager(documento.NameTable);
        espacios.AddNamespace("cac", Cac);
        espacios.AddNamespace("cbc", Cbc);

        var remitente = documento.DocumentElement!.SelectSingleNode("cac:DespatchSupplierParty", espacios)
            ?? throw new ArgumentException("El XML no tiene cac:DespatchSupplierParty.");
        var ruc = remitente.SelectSingleNode("cac:Party/cac:PartyIdentification/cbc:ID", espacios)?.InnerText ?? "";
        var nombre = remitente.SelectSingleNode("cac:Party/cac:PartyLegalEntity/cbc:RegistrationName", espacios)?.InnerText ?? "";

        var firma = Elemento(documento, "cac", Cac, "Signature",
            Elemento(documento, "cbc", Cbc, "ID", texto: IdFirma),
            Elemento(documento, "cac", Cac, "SignatoryParty",
                Elemento(documento, "cac", Cac, "PartyIdentification",
                    Elemento(documento, "cbc", Cbc, "ID", texto: ruc)),
                Elemento(documento, "cac", Cac, "PartyName",
                    Elemento(documento, "cbc", Cbc, "Name", texto: nombre))),
            Elemento(documento, "cac", Cac, "DigitalSignatureAttachment",
                Elemento(documento, "cac", Cac, "ExternalReference",
                    Elemento(documento, "cbc", Cbc, "URI", texto: "#" + IdFirma))));

        documento.DocumentElement.InsertBefore(firma, remitente);
    }

    private static XmlElement Elemento(XmlDocument documento, string prefijo, string espacio, string nombre,
        params XmlElement[] hijos) => Elemento(documento, prefijo, espacio, nombre, null, hijos);

    private static XmlElement Elemento(XmlDocument documento, string prefijo, string espacio, string nombre, string? texto,
        params XmlElement[] hijos)
    {
        var elemento = documento.CreateElement(prefijo, nombre, espacio);
        if (texto is not null)
            elemento.InnerText = texto;
        foreach (var hijo in hijos)
            elemento.AppendChild(hijo);
        return elemento;
    }

    /// <summary>Carga sin DTD ni entidades externas y conservando los espacios: la firma depende de cada byte.</summary>
    private static XmlDocument Cargar(string xml)
    {
        var documento = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        var configuracion = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var lector = XmlReader.Create(new StringReader(xml), configuracion);
        documento.Load(lector);
        return documento;
    }
}
