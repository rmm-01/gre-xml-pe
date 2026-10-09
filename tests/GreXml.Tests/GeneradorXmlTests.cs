using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using GreXml.Core.Modelo;
using GreXml.Core.Xml;

namespace GreXml.Tests;

/// <summary>Lee el XML generado con XPath, como lo haría SUNAT, y comprueba cada dato en su tag.</summary>
public class GeneradorXmlTests
{
    private static readonly XmlNamespaceManager Ns = CrearNamespaces();

    [Fact]
    public void Raiz_EsDespatchAdviceUbl21_TipoGre09()
    {
        var xml = GeneradorXml.Generar(GuiaEjemplo.VentaConTransportePrivado());

        Assert.Equal(GeneradorXml.Raiz + "DespatchAdvice", xml.Root!.Name);
        Assert.Equal("2.1", Valor(xml, "/d:DespatchAdvice/cbc:UBLVersionID"));
        Assert.Equal("2.0", Valor(xml, "/d:DespatchAdvice/cbc:CustomizationID"));
        Assert.Equal("09", Valor(xml, "/d:DespatchAdvice/cbc:DespatchAdviceTypeCode"));
        Assert.Equal("urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo01",
            Atributo(xml, "/d:DespatchAdvice/cbc:DespatchAdviceTypeCode", "listURI"));
    }

    [Fact]
    public void Cabecera_SerieNumeroFechaYHora()
    {
        var xml = GeneradorXml.Generar(GuiaEjemplo.VentaConTransportePrivado() with { Numero = 154 });

        Assert.Equal("T001-154", Valor(xml, "/d:DespatchAdvice/cbc:ID"));
        Assert.Equal("2026-10-09", Valor(xml, "/d:DespatchAdvice/cbc:IssueDate"));
        Assert.Equal("10:30:00", Valor(xml, "/d:DespatchAdvice/cbc:IssueTime"));
    }

    [Fact]
    public void Remitente_RucConCatalogo06YRazonSocial()
    {
        var xml = GeneradorXml.Generar(GuiaEjemplo.VentaConTransportePrivado());
        const string parte = "/d:DespatchAdvice/cac:DespatchSupplierParty/cac:Party";

        Assert.Equal(GuiaEjemplo.RucRemitente, Valor(xml, $"{parte}/cac:PartyIdentification/cbc:ID"));
        Assert.Equal("6", Atributo(xml, $"{parte}/cac:PartyIdentification/cbc:ID", "schemeID"));
        Assert.Equal("DISTRIBUIDORA EJEMPLO SAC", Valor(xml, $"{parte}/cac:PartyLegalEntity/cbc:RegistrationName"));
    }

    [Fact]
    public void Destinatario_ConDni_UsaSchemeId1()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with
        {
            Destinatario = new Destinatario(TiposDocumento.Dni, "12345678", "ANA MARIA QUISPE ROJAS"),
        };
        var xml = GeneradorXml.Generar(guia);
        const string id = "/d:DespatchAdvice/cac:DeliveryCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID";

        Assert.Equal("12345678", Valor(xml, id));
        Assert.Equal("1", Atributo(xml, id, "schemeID"));
    }

    [Fact]
    public void Envio_MotivoPesoYFechaDeInicio()
    {
        var xml = GeneradorXml.Generar(GuiaEjemplo.VentaConTransportePrivado());
        const string envio = "/d:DespatchAdvice/cac:Shipment";

        Assert.Equal("01", Valor(xml, $"{envio}/cbc:HandlingCode"));
        Assert.Equal("Venta", Valor(xml, $"{envio}/cbc:HandlingInstructions"));
        Assert.Equal("1250.5", Valor(xml, $"{envio}/cbc:GrossWeightMeasure"));
        Assert.Equal("KGM", Atributo(xml, $"{envio}/cbc:GrossWeightMeasure", "unitCode"));
        Assert.Equal("2026-10-10", Valor(xml, $"{envio}/cac:ShipmentStage/cac:TransitPeriod/cbc:StartDate"));
    }

    [Fact]
    public void Envio_PartidaYLlegadaConUbigeo()
    {
        var xml = GeneradorXml.Generar(GuiaEjemplo.VentaConTransportePrivado());
        const string entrega = "/d:DespatchAdvice/cac:Shipment/cac:Delivery";

        Assert.Equal("040101", Valor(xml, $"{entrega}/cac:DeliveryAddress/cbc:ID"));
        Assert.Equal("CALLE FICTICIA 456, AREQUIPA", Valor(xml, $"{entrega}/cac:DeliveryAddress/cac:AddressLine/cbc:Line"));
        Assert.Equal("150101", Valor(xml, $"{entrega}/cac:Despatch/cac:DespatchAddress/cbc:ID"));
        Assert.Equal("PE:INEI", Atributo(xml, $"{entrega}/cac:Despatch/cac:DespatchAddress/cbc:ID", "schemeAgencyName"));
    }

    [Fact]
    public void TransportePrivado_Modalidad02_VehiculoYConductorPrincipal()
    {
        var xml = GeneradorXml.Generar(GuiaEjemplo.VentaConTransportePrivado());
        const string etapa = "/d:DespatchAdvice/cac:Shipment/cac:ShipmentStage";

        Assert.Equal("02", Valor(xml, $"{etapa}/cbc:TransportModeCode"));
        Assert.Equal("ABC123", Valor(xml, "/d:DespatchAdvice/cac:Shipment/cac:TransportHandlingUnit/cac:TransportEquipment/cbc:ID"));
        Assert.Equal("12345678", Valor(xml, $"{etapa}/cac:DriverPerson/cbc:ID"));
        Assert.Equal("JUAN CARLOS", Valor(xml, $"{etapa}/cac:DriverPerson/cbc:FirstName"));
        Assert.Equal("PEREZ GOMEZ", Valor(xml, $"{etapa}/cac:DriverPerson/cbc:FamilyName"));
        Assert.Equal("Principal", Valor(xml, $"{etapa}/cac:DriverPerson/cbc:JobTitle"));
        Assert.Equal("Q12345678", Valor(xml, $"{etapa}/cac:DriverPerson/cac:IdentityDocumentReference/cbc:ID"));
        Assert.Null(xml.XPathSelectElement($"{etapa}/cac:CarrierParty", Ns));
    }

    [Fact]
    public void TransportePrivado_VariosVehiculosYConductores_PrincipalYSecundarios()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado();
        guia = guia with
        {
            Transporte = guia.Transporte with
            {
                Vehiculos = ["ABC123", "XYZ789"],
                Conductores = [.. guia.Transporte.Conductores, new Conductor(TiposDocumento.Dni, "87654321", "LUIS", "RAMOS", "Q87654321")],
            },
        };
        var xml = GeneradorXml.Generar(guia);

        Assert.Equal("XYZ789", Valor(xml,
            "/d:DespatchAdvice/cac:Shipment/cac:TransportHandlingUnit/cac:TransportEquipment/cac:AttachedTransportEquipment/cbc:ID"));
        Assert.Equal(["Principal", "Secundario"],
            xml.XPathSelectElements("//cac:DriverPerson/cbc:JobTitle", Ns).Select(e => e.Value));
    }

    [Fact]
    public void TransportePublico_Modalidad01_TransportistaSinVehiculoNiConductor()
    {
        var xml = GeneradorXml.Generar(GuiaEjemplo.VentaConTransportePublico());
        const string etapa = "/d:DespatchAdvice/cac:Shipment/cac:ShipmentStage";

        Assert.Equal("01", Valor(xml, $"{etapa}/cbc:TransportModeCode"));
        Assert.Equal(GuiaEjemplo.RucTransportista, Valor(xml, $"{etapa}/cac:CarrierParty/cac:PartyIdentification/cbc:ID"));
        Assert.Equal("TRANSPORTES INVENTADOS SAC", Valor(xml, $"{etapa}/cac:CarrierParty/cac:PartyLegalEntity/cbc:RegistrationName"));
        // SUNAT rechaza datos de vehículo o conductor en transporte público (error 2774).
        Assert.Null(xml.XPathSelectElement($"{etapa}/cac:DriverPerson", Ns));
        Assert.Null(xml.XPathSelectElement("//cac:TransportHandlingUnit", Ns));
    }

    [Fact]
    public void Bienes_UnaLineaPorBien_NumeradasDesde1()
    {
        var xml = GeneradorXml.Generar(GuiaEjemplo.VentaConTransportePrivado());
        var lineas = xml.XPathSelectElements("/d:DespatchAdvice/cac:DespatchLine", Ns).ToList();

        Assert.Equal(2, lineas.Count);
        Assert.Equal("1", Valor(lineas[0], "cbc:ID"));
        Assert.Equal("20", Valor(lineas[0], "cbc:DeliveredQuantity"));
        Assert.Equal("NIU", Atributo(lineas[0], "cbc:DeliveredQuantity", "unitCode"));
        Assert.Equal("CEMENTO PORTLAND TIPO I BOLSA 42.5 KG", Valor(lineas[0], "cac:Item/cbc:Description"));
        Assert.Equal("CEM-001", Valor(lineas[0], "cac:Item/cac:SellersItemIdentification/cbc:ID"));
        Assert.Equal("2", Valor(lineas[1], "cbc:ID"));
        Assert.Equal("2", Valor(lineas[1], "cac:OrderLineReference/cbc:LineID"));
        // El segundo bien no tiene código: el tag no se escribe vacío.
        Assert.Null(lineas[1].XPathSelectElement("cac:Item/cac:SellersItemIdentification", Ns));
    }

    [Fact]
    public void Observaciones_SoloSiExisten()
    {
        var sin = GeneradorXml.Generar(GuiaEjemplo.VentaConTransportePrivado());
        var con = GeneradorXml.Generar(GuiaEjemplo.VentaConTransportePrivado() with { Observaciones = "Entregar en almacén 2" });

        Assert.Null(sin.XPathSelectElement("/d:DespatchAdvice/cbc:Note", Ns));
        Assert.Equal("Entregar en almacén 2", Valor(con, "/d:DespatchAdvice/cbc:Note"));
    }

    [Fact]
    public void Texto_CaracteresEspeciales_SeEscapanYSeRecuperanIguales()
    {
        var guia = GuiaEjemplo.VentaConTransportePrivado() with
        {
            Destinatario = new Destinatario(TiposDocumento.Ruc, GuiaEjemplo.RucDestinatario, "PEREZ & HIJOS <SAC> \"ÑAÑU\""),
        };

        var texto = GeneradorXml.GenerarTexto(guia);

        Assert.Contains("PEREZ &amp; HIJOS &lt;SAC&gt;", texto);
        var leido = XDocument.Parse(texto);
        Assert.Equal("PEREZ & HIJOS <SAC> \"ÑAÑU\"",
            Valor(leido, "/d:DespatchAdvice/cac:DeliveryCustomerParty/cac:Party/cac:PartyLegalEntity/cbc:RegistrationName"));
    }

    [Fact]
    public void Texto_DeclaracionUtf8_SinBom()
    {
        var texto = GeneradorXml.GenerarTexto(GuiaEjemplo.VentaConTransportePrivado());

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", texto);
        Assert.NotEqual('﻿', texto[0]);
    }

    [Fact]
    public void Numeros_UsanPuntoDecimal_AunqueLaCulturaSeaEspanol()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // En es-PE el separador decimal podría ser coma; el XML siempre lleva punto.
            CultureInfo.CurrentCulture = new CultureInfo("es-ES");
            var xml = GeneradorXml.Generar(GuiaEjemplo.VentaConTransportePrivado());

            Assert.Equal("1250.5", Valor(xml, "/d:DespatchAdvice/cac:Shipment/cbc:GrossWeightMeasure"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static string Valor(XNode nodo, string xpath) =>
        (nodo.XPathSelectElement(xpath, Ns) ?? throw new Xunit.Sdk.XunitException($"No existe {xpath}")).Value;

    private static string? Atributo(XNode nodo, string xpath, string atributo) =>
        nodo.XPathSelectElement(xpath, Ns)?.Attribute(atributo)?.Value;

    private static XmlNamespaceManager CrearNamespaces()
    {
        var ns = new XmlNamespaceManager(new NameTable());
        ns.AddNamespace("d", GeneradorXml.Raiz.NamespaceName);
        ns.AddNamespace("cac", GeneradorXml.Cac.NamespaceName);
        ns.AddNamespace("cbc", GeneradorXml.Cbc.NamespaceName);
        return ns;
    }
}
