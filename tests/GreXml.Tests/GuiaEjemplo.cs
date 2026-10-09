using GreXml.Core.Modelo;

namespace GreXml.Tests;

/// <summary>Guías válidas con datos inventados. Cada prueba rompe una sola cosa con <c>with</c>.</summary>
internal static class GuiaEjemplo
{
    public const string RucRemitente = "20100000050";
    public const string RucDestinatario = "20100000068";
    public const string RucTransportista = "20100000076";

    public static GuiaRemision VentaConTransportePrivado() => new()
    {
        Serie = "T001",
        Numero = 1,
        FechaEmision = new DateOnly(2026, 10, 9),
        HoraEmision = new TimeOnly(10, 30, 0),
        Remitente = new Contribuyente(RucRemitente, "DISTRIBUIDORA EJEMPLO SAC"),
        Destinatario = new Destinatario(TiposDocumento.Ruc, RucDestinatario, "COMERCIAL DE PRUEBA EIRL"),
        MotivoTraslado = MotivosTraslado.Venta,
        FechaInicioTraslado = new DateOnly(2026, 10, 10),
        PesoBrutoTotal = 1250.5m,
        Partida = new PuntoTraslado("150101", "AV. LOS EJEMPLOS 123, LIMA"),
        Llegada = new PuntoTraslado("040101", "CALLE FICTICIA 456, AREQUIPA"),
        Bienes =
        [
            new Bien("CEMENTO PORTLAND TIPO I BOLSA 42.5 KG", 20, "NIU", "CEM-001"),
            new Bien("FIERRO CORRUGADO 1/2 PULGADA", 500, "KGM"),
        ],
        Transporte = new Transporte
        {
            Modalidad = ModalidadTransporte.Privado,
            Vehiculos = ["ABC123"],
            Conductores = [new Conductor(TiposDocumento.Dni, "12345678", "JUAN CARLOS", "PEREZ GOMEZ", "Q12345678")],
        },
    };

    public static GuiaRemision VentaConTransportePublico() => VentaConTransportePrivado() with
    {
        Transporte = new Transporte
        {
            Modalidad = ModalidadTransporte.Publico,
            Transportista = new Contribuyente(RucTransportista, "TRANSPORTES INVENTADOS SAC"),
        },
    };
}
