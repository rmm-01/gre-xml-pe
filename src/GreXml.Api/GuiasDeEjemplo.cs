using GreXml.Core.Modelo;

namespace GreXml.Api;

/// <summary>Guías válidas con datos inventados, para probar la API sin escribir 30 campos.</summary>
public static class GuiasDeEjemplo
{
    private const string RucRemitente = "20100000050";
    private const string RazonSocialRemitente = "DISTRIBUIDORA EJEMPLO SAC";

    public static GuiaRemision Para(string motivo, DateOnly hoy) => motivo switch
    {
        MotivosTraslado.Venta => Venta(hoy),
        MotivosTraslado.Compra => Venta(hoy) with
        {
            MotivoTraslado = MotivosTraslado.Compra,
            Destinatario = new Destinatario(TiposDocumento.Ruc, RucRemitente, RazonSocialRemitente),
            Proveedor = new Contribuyente("20100000084", "FABRICA INVENTADA SA"),
            Partida = new PuntoTraslado("150103", "JR. DEL PROVEEDOR 789, ATE"),
            Llegada = new PuntoTraslado("150101", "AV. LOS EJEMPLOS 123, LIMA"),
        },
        MotivosTraslado.TrasladoEntreEstablecimientos => Venta(hoy) with
        {
            MotivoTraslado = MotivosTraslado.TrasladoEntreEstablecimientos,
            Destinatario = new Destinatario(TiposDocumento.Ruc, RucRemitente, RazonSocialRemitente),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, "Motivo sin ejemplo."),
    };

    private static GuiaRemision Venta(DateOnly hoy) => new()
    {
        Serie = "T001",
        Numero = 1,
        FechaEmision = hoy,
        HoraEmision = new TimeOnly(9, 0),
        Remitente = new Contribuyente(RucRemitente, RazonSocialRemitente),
        Destinatario = new Destinatario(TiposDocumento.Ruc, "20100000068", "COMERCIAL DE PRUEBA EIRL"),
        MotivoTraslado = MotivosTraslado.Venta,
        FechaInicioTraslado = hoy,
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
}
