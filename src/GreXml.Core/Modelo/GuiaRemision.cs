namespace GreXml.Core.Modelo;

/// <summary>
/// Guía de Remisión Electrónica del remitente: el documento que acompaña a los bienes durante el traslado.
/// </summary>
public sealed record GuiaRemision
{
    /// <summary>Serie de 4 caracteres que empieza con "T" (por ejemplo, "T001").</summary>
    public required string Serie { get; init; }

    public required int Numero { get; init; }

    public required DateOnly FechaEmision { get; init; }

    public required TimeOnly HoraEmision { get; init; }

    /// <summary>Quien emite la guía y envía los bienes.</summary>
    public required Contribuyente Remitente { get; init; }

    /// <summary>Quien recibe los bienes.</summary>
    public required Destinatario Destinatario { get; init; }

    /// <summary>Quien vendió los bienes. Solo en el motivo 02 (compra), donde el remitente es el comprador.</summary>
    public Contribuyente? Proveedor { get; init; }

    /// <summary>Código del catálogo 20 de SUNAT (por ejemplo, "01" = venta). Ver <see cref="MotivosTraslado"/>.</summary>
    public required string MotivoTraslado { get; init; }

    public required DateOnly FechaInicioTraslado { get; init; }

    public required decimal PesoBrutoTotal { get; init; }

    /// <summary>"KGM" (kilogramos).</summary>
    public string UnidadPeso { get; init; } = UnidadesPeso.Kilogramos;

    public required PuntoTraslado Partida { get; init; }

    public required PuntoTraslado Llegada { get; init; }

    public required IReadOnlyList<Bien> Bienes { get; init; }

    public required Transporte Transporte { get; init; }

    public string? Observaciones { get; init; }
}

public sealed record Contribuyente(string Ruc, string RazonSocial);

/// <summary>Destinatario identificado con un documento del catálogo 06 (RUC, DNI, carné de extranjería o pasaporte).</summary>
public sealed record Destinatario(string TipoDocumento, string NumeroDocumento, string Nombre);

/// <summary>Lugar de partida o de llegada: ubigeo INEI de 6 dígitos y dirección.</summary>
public sealed record PuntoTraslado(string Ubigeo, string Direccion);

/// <summary>Bien trasladado. La unidad usa el catálogo 03 (por ejemplo, "NIU" = unidad, "KGM" = kilogramo).</summary>
public sealed record Bien(string Descripcion, decimal Cantidad, string UnidadMedida, string? Codigo = null);

/// <summary>
/// Público: lo traslada una empresa de transporte (se informa el transportista).
/// Privado: lo traslada el propio remitente (se informan vehículos y conductores).
/// </summary>
public sealed record Transporte
{
    public required ModalidadTransporte Modalidad { get; init; }

    public Contribuyente? Transportista { get; init; }

    /// <summary>Placas sin guion (por ejemplo, "ABC123").</summary>
    public IReadOnlyList<string> Vehiculos { get; init; } = [];

    public IReadOnlyList<Conductor> Conductores { get; init; } = [];
}

public enum ModalidadTransporte
{
    Publico,
    Privado,
}

public sealed record Conductor(string TipoDocumento, string NumeroDocumento, string Nombres, string Apellidos, string Licencia);
