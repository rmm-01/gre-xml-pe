using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using GreXml.Core.Xml;

namespace GreXml.Core.Envio;

/// <summary>
/// Imita a SUNAT sin red: revisa el archivo como lo haría SUNAT y responde con ticket y CDR en el mismo formato.
/// Nunca emite una guía real. Los códigos de error son los del catálogo de errores de SUNAT.
/// </summary>
/// <param name="certificadoComunicado">El certificado que el remitente "registró" en SUNAT; solo se aceptan firmas suyas.</param>
/// <param name="consultasEnProceso">Cuántas consultas del ticket responden "98" antes de dar el resultado.</param>
public sealed partial class EnvioSunatSimulado(X509Certificate2 certificadoComunicado, TimeProvider reloj, int consultasEnProceso = 1)
    : IEnvioSunat
{
    public const string RucSunat = "20131312955";

    /// <summary>Regla propia del simulador: estas placas "no figuran" en el registro de vehículos.</summary>
    public const string PrefijoPlacaNoRegistrada = "ZZZ";

    private readonly ConcurrentDictionary<string, Proceso> _procesos = new();
    private readonly ConcurrentDictionary<string, byte> _aceptadas = new();

    public Task<Ticket> EnviarAsync(SolicitudEnvio solicitud, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var nombre = NombreZip().Match(solicitud.NomArchivo);
        if (!nombre.Success)
            throw new EnvioSunatException("0151", "El nombre del archivo ZIP es incorrecto");

        var zip = DecodificarZip(solicitud);
        var archivos = LeerArchivos(zip);
        if (archivos.Count == 0)
            throw new EnvioSunatException("0155", "El archivo ZIP esta vacio");
        if (archivos.Count > 1)
            throw new EnvioSunatException("0158", "El archivo ZIP contiene demasiados comprobantes para este tipo de envío");

        var nombreBase = solicitud.NomArchivo[..^".zip".Length];
        if (archivos[0].Nombre != nombreBase + ".xml")
            throw new EnvioSunatException("0161", "El nombre del archivo XML no coincide con el nombre del archivo ZIP");

        if (_aceptadas.ContainsKey(nombreBase))
            throw new EnvioSunatException("2223", "El archivo ya fue presentado anteriormente");

        var resultado = Procesar(nombre, archivos[0].Contenido);
        if (resultado.CodRespuesta == EstadoEnvio.Terminado)
            _aceptadas.TryAdd(nombreBase, 0);

        var ticket = new Ticket(Guid.NewGuid().ToString(), reloj.GetUtcNow());
        _procesos[ticket.NumTicket] = new Proceso(resultado);
        return Task.FromResult(ticket);
    }

    public Task<EstadoEnvio> ConsultarAsync(string numTicket, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_procesos.TryGetValue(numTicket, out var proceso))
            throw new EnvioSunatException("0127", "El ticket no existe");

        var consulta = Interlocked.Increment(ref proceso.Consultas);
        return Task.FromResult(consulta <= consultasEnProceso ? new EstadoEnvio(EstadoEnvio.EnProceso) : proceso.Resultado);
    }

    private static byte[] DecodificarZip(SolicitudEnvio solicitud)
    {
        byte[] zip;
        try
        {
            zip = Convert.FromBase64String(solicitud.ArcGreZip);
        }
        catch (FormatException)
        {
            throw new EnvioSunatException("0156", "El archivo ZIP esta corrupto");
        }

        // El hash viaja aparte del ZIP: si no coincide, el archivo se dañó en el camino.
        if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(zip)), solicitud.HashZip, StringComparison.OrdinalIgnoreCase))
            throw new EnvioSunatException("0156", "El archivo ZIP esta corrupto");

        return zip;
    }

    private static IReadOnlyList<(string Nombre, byte[] Contenido)> LeerArchivos(byte[] zip)
    {
        try
        {
            return ZipSeguro.Leer(zip);
        }
        catch (InvalidDataException)
        {
            throw new EnvioSunatException("0156", "El archivo ZIP esta corrupto");
        }
    }

    /// <summary>Lo que SUNAT revisa después de dar el ticket, en el mismo orden: formato, nombre, firma y datos.</summary>
    private EstadoEnvio Procesar(Match nombre, byte[] contenido)
    {
        string xml;
        XDocument documento;
        try
        {
            xml = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(contenido).TrimStart('﻿');
            documento = EmpaquetadorGre.LeerXml(xml);
        }
        catch (Exception ex) when (ex is XmlException or DecoderFallbackException)
        {
            return Excepcion("0306", "No se puede leer (parsear) el archivo XML");
        }

        if (ValidadorXsd.Validar(xml).Count > 0)
            return Excepcion("0305", "El sistema no puede procesar el archivo xml");

        var raiz = documento.Root!;
        var cac = GeneradorXml.Cac;
        var cbc = GeneradorXml.Cbc;
        var id = raiz.Element(cbc + "ID")!.Value;
        var ruc = raiz.Element(cac + "DespatchSupplierParty")?.Element(cac + "Party")
            ?.Element(cac + "PartyIdentification")?.Element(cbc + "ID")?.Value;

        // El nombre del archivo debe decir lo mismo que el contenido.
        if (ruc != nombre.Groups["ruc"].Value)
            return Excepcion("1034", "Número de RUC del nombre del archivo no coincide con el consignado en el contenido del archivo XML");
        var partes = id.Split('-');
        if (partes.Length != 2 || partes[0] != nombre.Groups["serie"].Value)
            return Excepcion("1035", "Numero de Serie del nombre del archivo no coincide con el consignado en el contenido del archivo XML");
        if (!int.TryParse(partes[1], out var numero) || numero != int.Parse(nombre.Groups["numero"].Value))
            return Excepcion("1036", "Número de documento en el nombre del archivo no coincide con el consignado en el contenido del XML");

        if (documento.Descendants(XNamespace.Get(SignedXml.XmlDsigNamespaceUrl) + "Signature").FirstOrDefault() is null)
            return Excepcion("1059", "El XML no contiene firma digital.");

        using (var certificado = FirmadorXml.ObtenerCertificado(xml))
        {
            if (certificado?.Thumbprint != certificadoComunicado.Thumbprint)
                return Rechazo(ruc, id, "2325", "El certificado usado no es el comunicado a SUNAT");
        }

        if (!FirmadorXml.Verificar(xml, certificadoComunicado))
            return Rechazo(ruc, id, "2335", "El documento electrónico ingresado ha sido alterado");

        var observaciones = raiz.Descendants(cac + "TransportEquipment")
            .SelectMany(e => e.Elements(cbc + "ID").Concat(e.Elements(cac + "AttachedTransportEquipment").Elements(cbc + "ID")))
            .Where(placa => placa.Value.StartsWith(PrefijoPlacaNoRegistrada, StringComparison.Ordinal))
            .Select(placa => $"4398 - El Numero de placa no se encuentra en las bases consultadas ({placa.Value})")
            .ToList();

        return new EstadoEnvio(EstadoEnvio.Terminado,
            Cdr(ruc, id, "0", $"La Guia de Remision numero {id}, ha sido aceptada", observaciones));
    }

    private static EstadoEnvio Excepcion(string codigo, string mensaje) =>
        new(EstadoEnvio.ConError, Error: new ErrorSunat(codigo, mensaje));

    private EstadoEnvio Rechazo(string ruc, string id, string codigo, string mensaje) =>
        new(EstadoEnvio.ConError, Cdr(ruc, id, codigo, mensaje, []));

    /// <summary>
    /// CDR con la estructura del real (ApplicationResponse dentro de un ZIP "R-...zip"), pero sin la firma de SUNAT.
    /// </summary>
    private byte[] Cdr(string ruc, string id, string codigo, string descripcion, IReadOnlyList<string> notas)
    {
        var cac = GeneradorXml.Cac;
        var cbc = GeneradorXml.Cbc;
        var ahora = reloj.GetUtcNow().ToOffset(TimeSpan.FromHours(-5)); // hora de Perú

        var respuesta = new XDocument(new XDeclaration("1.0", "UTF-8", null),
            new XElement(LectorCdr.Raiz + "ApplicationResponse",
                new XAttribute(XNamespace.Xmlns + "cac", cac),
                new XAttribute(XNamespace.Xmlns + "cbc", cbc),
                new XElement(cbc + "UBLVersionID", "2.0"),
                new XElement(cbc + "CustomizationID", "1.0"),
                new XElement(cbc + "ID", Guid.NewGuid().ToString("N")),
                new XElement(cbc + "IssueDate", ahora.ToString("yyyy-MM-dd")),
                new XElement(cbc + "IssueTime", ahora.ToString("HH:mm:ss")),
                new XElement(cbc + "ResponseDate", ahora.ToString("yyyy-MM-dd")),
                new XElement(cbc + "ResponseTime", ahora.ToString("HH:mm:ss")),
                notas.Select(n => new XElement(cbc + "Note", n)),
                new XElement(cac + "SenderParty",
                    new XElement(cac + "PartyIdentification", new XElement(cbc + "ID", RucSunat))),
                new XElement(cac + "ReceiverParty",
                    new XElement(cac + "PartyIdentification", new XElement(cbc + "ID", ruc))),
                new XElement(cac + "DocumentResponse",
                    new XElement(cac + "Response",
                        new XElement(cbc + "ReferenceID", id),
                        new XElement(cbc + "ResponseCode", codigo),
                        new XElement(cbc + "Description", descripcion)),
                    new XElement(cac + "DocumentReference",
                        new XElement(cbc + "ID", id)))));

        var nombre = $"R-{ruc}-{GeneradorXml.TipoDocumentoGre}-{id}.xml";
        return ZipSeguro.Crear(nombre, new UTF8Encoding(false).GetBytes(respuesta.Declaration + respuesta.ToString()));
    }

    // RUC (11 dígitos) - tipo 09 - serie T + 3 - número (1 a 8 dígitos) .zip
    [GeneratedRegex(@"^(?<ruc>\d{11})-09-(?<serie>T[A-Z0-9]{3})-(?<numero>\d{1,8})\.zip$")]
    private static partial Regex NombreZip();

    private sealed class Proceso(EstadoEnvio resultado)
    {
        public readonly EstadoEnvio Resultado = resultado;
        public int Consultas;
    }
}
