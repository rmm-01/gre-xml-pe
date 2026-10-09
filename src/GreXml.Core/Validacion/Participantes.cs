using GreXml.Core.Modelo;

namespace GreXml.Core.Validacion;

/// <summary>Comparaciones entre los participantes de la guía que usan las reglas de varios motivos.</summary>
internal static class Participantes
{
    public static bool DestinatarioEsRemitente(GuiaRemision guia) =>
        guia.Destinatario.TipoDocumento == TiposDocumento.Ruc && guia.Destinatario.NumeroDocumento == guia.Remitente.Ruc;
}
