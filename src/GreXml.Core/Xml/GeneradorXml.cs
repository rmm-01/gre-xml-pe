using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using GreXml.Core.Modelo;

namespace GreXml.Core.Xml;

/// <summary>
/// Convierte una guía en el XML UBL 2.1 (DespatchAdvice) que pide SUNAT para la GRE remitente.
/// Recibe una guía que ya pasó <see cref="Validacion.GuiaValidador"/>: aquí no se repiten las validaciones.
/// El orden de los elementos sigue el esquema UBL 2.1; cambiarlo hace que el XML no pase el XSD.
/// </summary>
public static class GeneradorXml
{
    public static readonly XNamespace Raiz = "urn:oasis:names:specification:ubl:schema:xsd:DespatchAdvice-2";
    public static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    public static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    public const string TipoDocumentoGre = "09";

    private const string Catalogos = "urn:pe:gob:sunat:cpe:see:gem:catalogos:";

    // Los montos y cantidades usan punto decimal, sin importar la cultura del servidor.
    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    public static XDocument Generar(GuiaRemision guia) => new(
        new XDeclaration("1.0", "UTF-8", null),
        new XElement(Raiz + "DespatchAdvice",
            new XAttribute(XNamespace.Xmlns + "cac", Cac),
            new XAttribute(XNamespace.Xmlns + "cbc", Cbc),
            new XElement(Cbc + "UBLVersionID", "2.1"),
            new XElement(Cbc + "CustomizationID", "2.0"),
            new XElement(Cbc + "ID", $"{guia.Serie}-{guia.Numero}"),
            new XElement(Cbc + "IssueDate", guia.FechaEmision.ToString("yyyy-MM-dd", Invariante)),
            new XElement(Cbc + "IssueTime", guia.HoraEmision.ToString("HH:mm:ss", Invariante)),
            new XElement(Cbc + "DespatchAdviceTypeCode",
                Catalogo("01", "Tipo de Documento"),
                TipoDocumentoGre),
            string.IsNullOrWhiteSpace(guia.Observaciones) ? null : new XElement(Cbc + "Note", guia.Observaciones),
            new XElement(Cac + "DespatchSupplierParty",
                Parte(TiposDocumento.Ruc, guia.Remitente.Ruc, guia.Remitente.RazonSocial)),
            new XElement(Cac + "DeliveryCustomerParty",
                Parte(guia.Destinatario.TipoDocumento, guia.Destinatario.NumeroDocumento, guia.Destinatario.Nombre)),
            guia.Proveedor is null
                ? null
                : new XElement(Cac + "SellerSupplierParty",
                    Parte(TiposDocumento.Ruc, guia.Proveedor.Ruc, guia.Proveedor.RazonSocial)),
            Envio(guia),
            guia.Bienes.Select((bien, i) => Linea(bien, i + 1))));

    /// <summary>XML como texto UTF-8 con declaración, listo para guardar o firmar.</summary>
    public static string GenerarTexto(GuiaRemision guia)
    {
        var configuracion = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true };
        using var memoria = new MemoryStream();
        using (var escritor = XmlWriter.Create(memoria, configuracion))
            Generar(guia).Save(escritor);

        return Encoding.UTF8.GetString(memoria.ToArray());
    }

    private static XElement Envio(GuiaRemision guia) =>
        new(Cac + "Shipment",
            // SUNAT pide este valor fijo como identificador del envío.
            new XElement(Cbc + "ID", "SUNAT_Envio"),
            new XElement(Cbc + "HandlingCode", Catalogo("20", "Motivo de traslado"), guia.MotivoTraslado),
            new XElement(Cbc + "HandlingInstructions", MotivosTraslado.Descripcion(guia.MotivoTraslado)),
            new XElement(Cbc + "GrossWeightMeasure",
                new XAttribute("unitCode", guia.UnidadPeso),
                guia.PesoBrutoTotal.ToString(Invariante)),
            Etapa(guia),
            new XElement(Cac + "Delivery",
                Direccion("DeliveryAddress", guia.Llegada),
                new XElement(Cac + "Despatch", Direccion("DespatchAddress", guia.Partida))),
            Vehiculos(guia.Transporte));

    private static XElement Etapa(GuiaRemision guia)
    {
        var transporte = guia.Transporte;
        var publico = transporte.Modalidad == ModalidadTransporte.Publico;

        return new XElement(Cac + "ShipmentStage",
            new XElement(Cbc + "TransportModeCode",
                Catalogo("18", "Modalidad de traslado"),
                publico ? "01" : "02"),
            new XElement(Cac + "TransitPeriod",
                new XElement(Cbc + "StartDate", guia.FechaInicioTraslado.ToString("yyyy-MM-dd", Invariante))),
            publico
                ? new XElement(Cac + "CarrierParty",
                    Identificacion(TiposDocumento.Ruc, transporte.Transportista!.Ruc),
                    new XElement(Cac + "PartyLegalEntity",
                        new XElement(Cbc + "RegistrationName", transporte.Transportista.RazonSocial)))
                : null,
            publico ? null : transporte.Conductores.Select((conductor, i) => Conductor(conductor, principal: i == 0)));
    }

    private static XElement Conductor(Conductor conductor, bool principal) =>
        new(Cac + "DriverPerson",
            DocumentoIdentidad(conductor.TipoDocumento, conductor.NumeroDocumento),
            new XElement(Cbc + "FirstName", conductor.Nombres),
            new XElement(Cbc + "FamilyName", conductor.Apellidos),
            // El primero de la lista es el conductor principal; los demás, secundarios.
            new XElement(Cbc + "JobTitle", principal ? "Principal" : "Secundario"),
            new XElement(Cac + "IdentityDocumentReference",
                new XElement(Cbc + "ID", conductor.Licencia)));

    /// <summary>Placa del vehículo principal; los demás van como vehículos secundarios dentro de él.</summary>
    private static XElement? Vehiculos(Transporte transporte)
    {
        if (transporte.Modalidad == ModalidadTransporte.Publico || transporte.Vehiculos.Count == 0)
            return null;

        return new XElement(Cac + "TransportHandlingUnit",
            new XElement(Cac + "TransportEquipment",
                new XElement(Cbc + "ID", transporte.Vehiculos[0]),
                transporte.Vehiculos.Skip(1).Select(placa =>
                    new XElement(Cac + "AttachedTransportEquipment", new XElement(Cbc + "ID", placa)))));
    }

    private static XElement Direccion(string nombre, PuntoTraslado punto) =>
        new(Cac + nombre,
            new XElement(Cbc + "ID",
                new XAttribute("schemeAgencyName", "PE:INEI"),
                new XAttribute("schemeName", "Ubigeos"),
                punto.Ubigeo),
            new XElement(Cac + "AddressLine",
                new XElement(Cbc + "Line", punto.Direccion)));

    private static XElement Linea(Bien bien, int numero) =>
        new(Cac + "DespatchLine",
            new XElement(Cbc + "ID", numero),
            new XElement(Cbc + "DeliveredQuantity",
                new XAttribute("unitCode", bien.UnidadMedida),
                new XAttribute("unitCodeListID", "UN/ECE rec 20"),
                new XAttribute("unitCodeListAgencyName", "United Nations Economic Commission for Europe"),
                bien.Cantidad.ToString(Invariante)),
            // UBL exige una referencia a la línea del pedido; se usa el mismo número de línea.
            new XElement(Cac + "OrderLineReference",
                new XElement(Cbc + "LineID", numero)),
            new XElement(Cac + "Item",
                new XElement(Cbc + "Description", bien.Descripcion),
                string.IsNullOrWhiteSpace(bien.Codigo)
                    ? null
                    : new XElement(Cac + "SellersItemIdentification", new XElement(Cbc + "ID", bien.Codigo))));

    private static XElement Parte(string tipoDocumento, string numero, string nombre) =>
        new(Cac + "Party",
            Identificacion(tipoDocumento, numero),
            new XElement(Cac + "PartyLegalEntity",
                new XElement(Cbc + "RegistrationName", nombre)));

    private static XElement Identificacion(string tipoDocumento, string numero) =>
        new(Cac + "PartyIdentification", DocumentoIdentidad(tipoDocumento, numero));

    /// <summary>Número de documento con su tipo del catálogo 06 en schemeID.</summary>
    private static XElement DocumentoIdentidad(string tipoDocumento, string numero) =>
        new(Cbc + "ID",
            new XAttribute("schemeID", tipoDocumento),
            new XAttribute("schemeName", "Documento de Identidad"),
            new XAttribute("schemeAgencyName", "PE:SUNAT"),
            new XAttribute("schemeURI", Catalogos + "catalogo06"),
            numero);

    /// <summary>Atributos que indican de qué catálogo SUNAT viene un código.</summary>
    private static XAttribute[] Catalogo(string numero, string nombre) =>
    [
        new("listAgencyName", "PE:SUNAT"),
        new("listName", nombre),
        new("listURI", Catalogos + "catalogo" + numero),
    ];
}
