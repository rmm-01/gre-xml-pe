using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace GreXml.Core.Xml;

/// <summary>Un problema que encontró el XSD: mensaje y posición en el XML.</summary>
public sealed record ErrorXsd(string Mensaje, int Linea, int Columna);

/// <summary>
/// Valida un XML contra el esquema oficial UBL 2.1 del DespatchAdvice (orden de elementos, tipos de dato,
/// elementos obligatorios). No revisa reglas de negocio: eso lo hace <see cref="Validacion.GuiaValidador"/>.
/// </summary>
public static class ValidadorXsd
{
    // Dirección ficticia: solo sirve para que los "../common/..." de los esquemas se resuelvan entre sí.
    // ResolutorEmbebido nunca la consulta en la red.
    private const string BaseEsquemas = "http://grexml.local/Esquemas/ubl-2.1/";

    // Compilar el conjunto de esquemas es lo costoso; se hace una sola vez y el resultado no cambia.
    private static readonly Lazy<XmlSchemaSet> Esquemas = new(CargarEsquemas);

    public static IReadOnlyList<ErrorXsd> Validar(string xml)
    {
        var errores = new List<ErrorXsd>();
        var configuracion = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = Esquemas.Value,
            // El XML viene de fuera: sin DTD ni entidades externas (protección contra XXE).
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings,
        };
        configuracion.ValidationEventHandler += (_, e) =>
            errores.Add(new ErrorXsd(e.Message, e.Exception?.LineNumber ?? 0, e.Exception?.LinePosition ?? 0));

        try
        {
            using var lector = XmlReader.Create(new StringReader(xml), configuracion);
            while (lector.Read())
            {
            }
        }
        catch (XmlException ex)
        {
            // XML mal formado: el XSD ni siquiera puede empezar.
            errores.Add(new ErrorXsd(ex.Message, ex.LineNumber, ex.LinePosition));
        }

        return errores;
    }

    public static IReadOnlyList<ErrorXsd> Validar(XDocument documento) => Validar(documento.ToString());

    private static XmlSchemaSet CargarEsquemas()
    {
        var resolutor = new ResolutorEmbebido();
        var conjunto = new XmlSchemaSet { XmlResolver = resolutor };
        var configuracion = new XmlReaderSettings
        {
            // El esquema de XMLDSig de OASIS trae un DOCTYPE interno; se permite solo aquí, con esquemas propios.
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = resolutor,
        };

        const string principal = BaseEsquemas + "maindoc/UBL-DespatchAdvice-2.1.xsd";
        using var lector = XmlReader.Create(resolutor.Abrir(new Uri(principal)), configuracion, principal);
        conjunto.Add(null, lector);
        conjunto.Compile();
        return conjunto;
    }

    /// <summary>Entrega los esquemas desde los recursos de la DLL en lugar de leer disco o internet.</summary>
    private sealed class ResolutorEmbebido : XmlResolver
    {
        private static readonly Assembly Ensamblado = typeof(ValidadorXsd).Assembly;

        // En Windows %(RecursiveDir) deja "\" en el nombre del recurso; se normaliza a "/".
        private static readonly Dictionary<string, string> Recursos = Ensamblado.GetManifestResourceNames()
            .ToDictionary(n => n.Replace('\\', '/'), n => n, StringComparer.OrdinalIgnoreCase);

        public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn) => Abrir(absoluteUri);

        public Stream Abrir(Uri uri)
        {
            var relativa = new Uri(BaseEsquemas).MakeRelativeUri(uri).ToString();
            if (!Recursos.TryGetValue($"Esquemas/ubl-2.1/{relativa}", out var nombre))
                throw new FileNotFoundException($"Esquema no incluido en la biblioteca: {uri}");

            return Ensamblado.GetManifestResourceStream(nombre)!;
        }
    }
}
