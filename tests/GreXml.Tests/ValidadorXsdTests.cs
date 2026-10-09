using System.Xml.Linq;
using GreXml.Core.Modelo;
using GreXml.Core.Xml;

namespace GreXml.Tests;

public class ValidadorXsdTests
{
    private static readonly XNamespace Cbc = GeneradorXml.Cbc;

    [Fact]
    public void XmlGenerado_TransportePrivado_CumpleElEsquema()
    {
        Assert.Empty(ValidadorXsd.Validar(GeneradorXml.GenerarTexto(GuiaEjemplo.VentaConTransportePrivado())));
    }

    [Fact]
    public void XmlGenerado_TransportePublico_CumpleElEsquema()
    {
        Assert.Empty(ValidadorXsd.Validar(GeneradorXml.GenerarTexto(GuiaEjemplo.VentaConTransportePublico())));
    }

    [Fact]
    public void XmlGenerado_ConTodosLosOpcionales_CumpleElEsquema()
    {
        // Observaciones, varios vehículos y conductores: los elementos que solo aparecen a veces.
        var guia = GuiaEjemplo.VentaConTransportePrivado();
        guia = guia with
        {
            Observaciones = "Entregar en almacén 2",
            Transporte = guia.Transporte with
            {
                Vehiculos = ["ABC123", "XYZ789"],
                Conductores = [.. guia.Transporte.Conductores, new Conductor(TiposDocumento.Dni, "87654321", "LUIS", "RAMOS", "Q87654321")],
            },
        };

        Assert.Empty(ValidadorXsd.Validar(GeneradorXml.Generar(guia)));
    }

    [Fact]
    public void ElementosEnOtroOrden_NoCumple()
    {
        var xml = GeneradorXml.Generar(GuiaEjemplo.VentaConTransportePrivado() with { Observaciones = "Nota" });
        // Mover cbc:Note antes de cbc:IssueDate: mismos datos, orden que el esquema no permite.
        var nota = xml.Root!.Element(Cbc + "Note")!;
        nota.Remove();
        xml.Root.Element(Cbc + "IssueDate")!.AddBeforeSelf(nota);

        var errores = ValidadorXsd.Validar(xml);

        Assert.NotEmpty(errores);
    }

    [Fact]
    public void FechaConFormatoPeruano_NoCumple()
    {
        var xml = GeneradorXml.Generar(GuiaEjemplo.VentaConTransportePrivado());
        xml.Root!.Element(Cbc + "IssueDate")!.Value = "09/10/2026";

        var error = Assert.Single(ValidadorXsd.Validar(xml));

        Assert.Contains("IssueDate", error.Mensaje);
    }

    [Fact]
    public void SinIdDelDocumento_NoCumple()
    {
        var xml = GeneradorXml.Generar(GuiaEjemplo.VentaConTransportePrivado());
        xml.Root!.Element(Cbc + "ID")!.Remove();

        Assert.NotEmpty(ValidadorXsd.Validar(xml));
    }

    [Fact]
    public void ElementoQueNoExisteEnUbl_NoCumple()
    {
        var xml = GeneradorXml.Generar(GuiaEjemplo.VentaConTransportePrivado());
        xml.Root!.Element(Cbc + "IssueDate")!.AddAfterSelf(new XElement(Cbc + "Inventado", "x"));

        Assert.NotEmpty(ValidadorXsd.Validar(xml));
    }

    [Fact]
    public void Error_IndicaLineaYColumna()
    {
        var texto = GeneradorXml.GenerarTexto(GuiaEjemplo.VentaConTransportePrivado())
            .Replace("<cbc:IssueDate>2026-10-09</cbc:IssueDate>", "<cbc:IssueDate>ayer</cbc:IssueDate>");
        var lineaEsperada = texto.Split('\n').ToList().FindIndex(l => l.Contains("ayer")) + 1;

        var error = Assert.Single(ValidadorXsd.Validar(texto));

        Assert.Equal(lineaEsperada, error.Linea);
        Assert.True(error.Columna > 0);
    }

    [Fact]
    public void XmlMalFormado_DevuelveErrorSinLanzar()
    {
        // Antes de llegar al final roto, el XSD ya reporta elementos desconocidos; el último error es el de formato.
        var errores = ValidadorXsd.Validar("<DespatchAdvice><sin-cerrar>");

        Assert.NotEmpty(errores);
        Assert.True(errores[^1].Linea > 0);
    }

    [Fact]
    public void XmlConDtd_SeRechaza()
    {
        // Un DOCTYPE con entidad externa es el inicio típico de un ataque XXE: no se procesa.
        const string xml = """
            <?xml version="1.0"?>
            <!DOCTYPE d [ <!ENTITY x SYSTEM "file:///c:/windows/win.ini"> ]>
            <DespatchAdvice xmlns="urn:oasis:names:specification:ubl:schema:xsd:DespatchAdvice-2">&x;</DespatchAdvice>
            """;

        var error = Assert.Single(ValidadorXsd.Validar(xml));

        Assert.Contains("DTD", error.Mensaje);
    }
}
