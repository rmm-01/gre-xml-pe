using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace GreXml.Core.Envio;

public enum EstadoCdr
{
    Aceptada,
    /// <summary>Aceptada, pero con advertencias (códigos 4000+). La guía es válida.</summary>
    Observada,
    /// <summary>Códigos 2000 a 3999: la guía no existe legalmente; hay que corregir y emitir otra.</summary>
    Rechazada,
}

public sealed record Observacion(string Codigo, string Descripcion);

public sealed record ResultadoCdr(string IdDocumento, string Codigo, string Descripcion, IReadOnlyList<Observacion> Observaciones)
{
    public EstadoCdr Estado => int.Parse(Codigo) switch
    {
        0 => Observaciones.Count > 0 ? EstadoCdr.Observada : EstadoCdr.Aceptada,
        >= 4000 => EstadoCdr.Observada,
        _ => EstadoCdr.Rechazada,
    };
}

/// <summary>
/// Lee el CDR (Constancia de Recepción): un ZIP con "R-{nombre}.xml", un ApplicationResponse UBL.
/// Sirve igual para el CDR simulado y para uno real de SUNAT.
/// </summary>
public static partial class LectorCdr
{
    public static readonly XNamespace Raiz = "urn:oasis:names:specification:ubl:schema:xsd:ApplicationResponse-2";

    public static ResultadoCdr Leer(byte[] cdrZip)
    {
        var (_, contenido) = ZipSeguro.Leer(cdrZip)
            .FirstOrDefault(a => Path.GetFileName(a.Nombre).StartsWith("R-", StringComparison.OrdinalIgnoreCase)
                && a.Nombre.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
        if (contenido is null)
            throw new InvalidDataException("El ZIP no contiene el XML de respuesta (R-*.xml).");

        var raiz = EmpaquetadorGre.LeerXml(new UTF8Encoding(false).GetString(contenido).TrimStart('﻿')).Root!;
        var cac = Xml.GeneradorXml.Cac;
        var cbc = Xml.GeneradorXml.Cbc;

        var documento = raiz.Element(cac + "DocumentResponse")
            ?? throw new InvalidDataException("El CDR no tiene cac:DocumentResponse.");
        var respuesta = documento.Element(cac + "Response");
        var codigo = respuesta?.Element(cbc + "ResponseCode")?.Value.Trim();
        if (string.IsNullOrEmpty(codigo) || !int.TryParse(codigo, out _))
            throw new InvalidDataException("El CDR no tiene un cbc:ResponseCode numérico.");

        // Cada observación llega como "4398 - descripción".
        var observaciones = raiz.Elements(cbc + "Note")
            .Select(n => NotaConCodigo().Match(n.Value) is { Success: true } m
                ? new Observacion(m.Groups[1].Value, m.Groups[2].Value.Trim())
                : new Observacion("", n.Value.Trim()))
            .ToList();

        return new ResultadoCdr(
            documento.Element(cac + "DocumentReference")?.Element(cbc + "ID")?.Value ?? "",
            codigo,
            respuesta!.Element(cbc + "Description")?.Value ?? "",
            observaciones);
    }

    [GeneratedRegex(@"^\s*(\d{4})\s*-\s*(.*)$", RegexOptions.Singleline)]
    private static partial Regex NotaConCodigo();
}
