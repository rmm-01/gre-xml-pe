using System.Text;
using System.Xml;
using System.Xml.Linq;
using GreXml.Core.Xml;

namespace GreXml.Core.Envio;

/// <summary>Arma el ZIP de la guía firmada. El nombre sale del propio XML, así nombre y contenido siempre coinciden.</summary>
public static class EmpaquetadorGre
{
    public static PaqueteGre Empaquetar(string xmlFirmado)
    {
        var raiz = LeerXml(xmlFirmado).Root!;
        var cac = GeneradorXml.Cac;
        var cbc = GeneradorXml.Cbc;

        var id = raiz.Element(cbc + "ID")?.Value
            ?? throw new ArgumentException("El XML no tiene cbc:ID (serie-número).", nameof(xmlFirmado));
        var ruc = raiz.Element(cac + "DespatchSupplierParty")?.Element(cac + "Party")
                ?.Element(cac + "PartyIdentification")?.Element(cbc + "ID")?.Value
            ?? throw new ArgumentException("El XML no tiene el RUC del remitente.", nameof(xmlFirmado));

        return Empaquetar($"{ruc}-{GeneradorXml.TipoDocumentoGre}-{id}", xmlFirmado);
    }

    /// <summary>
    /// ZIP con un único archivo "{nombreBase}.xml". Los bytes del XML se guardan tal cual: cualquier cambio,
    /// incluso de espacios, invalidaría la firma.
    /// </summary>
    public static PaqueteGre Empaquetar(string nombreBase, string xml) =>
        new(nombreBase, ZipSeguro.Crear(nombreBase + ".xml", new UTF8Encoding(false).GetBytes(xml)));

    internal static XDocument LeerXml(string xml)
    {
        var configuracion = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var lector = XmlReader.Create(new StringReader(xml), configuracion);
        return XDocument.Load(lector, LoadOptions.PreserveWhitespace);
    }
}
