using System.Text.RegularExpressions;
using GreXml.Core.Modelo;
using R = GreXml.Core.Validacion.CatalogoReglas;

namespace GreXml.Core.Validacion;

/// <summary>
/// Revisa una guía antes de generar el XML y devuelve todos los errores juntos, no solo el primero:
/// así quien llena el formulario corrige todo de una vez.
/// </summary>
public sealed partial class GuiaValidador
{
    public const int MaxDescripcionBien = 500;
    public const int MaxObservaciones = 250;

    private readonly Dictionary<string, IReglasMotivo> _reglasPorMotivo;

    public GuiaValidador(IEnumerable<IReglasMotivo> reglasPorMotivo)
    {
        _reglasPorMotivo = reglasPorMotivo.ToDictionary(r => r.Motivo);
    }

    /// <summary>Validador con todos los motivos que el proyecto soporta.</summary>
    public static GuiaValidador ConMotivosSoportados() =>
        new([new ReglasVenta(), new ReglasCompra(), new ReglasTrasladoEstablecimientos()]);

    public IReadOnlyList<ErrorValidacion> Validar(GuiaRemision guia)
    {
        var errores = new List<ErrorValidacion>();

        ValidarCabecera(guia, errores);
        ValidarTraslado(guia, errores);
        ValidarBienes(guia.Bienes, errores);
        ValidarTransporte(guia, errores);

        // Las reglas del motivo solo se aplican si el motivo es uno de los soportados.
        if (_reglasPorMotivo.TryGetValue(guia.MotivoTraslado, out var reglasMotivo))
        {
            ValidarProveedor(guia, reglasMotivo.LlevaProveedor, errores);
            errores.AddRange(reglasMotivo.Validar(guia));
        }
        else
            errores.Add(ErrorValidacion.De(R.MotivoNoSoportado, "motivoTraslado",
                $"Motivo de traslado no soportado. Soportados: {string.Join(", ", _reglasPorMotivo.Keys)}."));

        return errores;
    }

    private static void ValidarCabecera(GuiaRemision guia, List<ErrorValidacion> errores)
    {
        if (guia.Serie is null || !SerieGre().IsMatch(guia.Serie))
            errores.Add(ErrorValidacion.De(R.SerieInvalida, "serie",
                "La serie debe tener 4 caracteres: una T seguida de 3 letras o números (por ejemplo, T001)."));

        if (guia.Numero is < 1 or > 99_999_999)
            errores.Add(ErrorValidacion.De(R.NumeroInvalido, "numero", "El número debe estar entre 1 y 99999999."));

        if (!DocumentoIdentidad.EsRucValido(guia.Remitente.Ruc))
            errores.Add(ErrorValidacion.De(R.RucRemitenteInvalido, "remitente.ruc", "El RUC del remitente no es válido."));

        if (string.IsNullOrWhiteSpace(guia.Remitente.RazonSocial))
            errores.Add(ErrorValidacion.De(R.RazonSocialRemitenteObligatoria, "remitente.razonSocial",
                "La razón social del remitente es obligatoria."));

        var destinatario = guia.Destinatario;
        ValidarDocumento(destinatario.TipoDocumento, destinatario.NumeroDocumento, "destinatario",
            R.TipoDocumentoDestinatarioInvalido, R.DocumentoDestinatarioInvalido, errores);

        if (string.IsNullOrWhiteSpace(destinatario.Nombre))
            errores.Add(ErrorValidacion.De(R.NombreDestinatarioObligatorio, "destinatario.nombre",
                "El nombre o razón social del destinatario es obligatorio."));

        if (guia.Observaciones?.Length > MaxObservaciones)
            errores.Add(ErrorValidacion.De(R.ObservacionesDemasiadoLargas, "observaciones",
                $"Las observaciones admiten hasta {MaxObservaciones} caracteres."));
    }

    /// <summary>Si el motivo lo pide, el proveedor es obligatorio y con datos válidos; si no, no debe venir.</summary>
    private static void ValidarProveedor(GuiaRemision guia, bool llevaProveedor, List<ErrorValidacion> errores)
    {
        var proveedor = guia.Proveedor;

        if (!llevaProveedor)
        {
            if (proveedor is not null)
                errores.Add(ErrorValidacion.De(R.ProveedorNoCorresponde, "proveedor",
                    "Para este motivo de traslado la guía no debe contener datos del proveedor."));
            return;
        }

        if (proveedor is null)
        {
            errores.Add(ErrorValidacion.De(R.ProveedorObligatorio, "proveedor", "Para este motivo de traslado se debe indicar el proveedor."));
            return;
        }

        if (!DocumentoIdentidad.EsRucValido(proveedor.Ruc))
            errores.Add(ErrorValidacion.De(R.RucProveedorInvalido, "proveedor.ruc", "El RUC del proveedor no es válido."));

        if (string.IsNullOrWhiteSpace(proveedor.RazonSocial))
            errores.Add(ErrorValidacion.De(R.RazonSocialProveedorObligatoria, "proveedor.razonSocial",
                "La razón social del proveedor es obligatoria."));
    }

    private static void ValidarTraslado(GuiaRemision guia, List<ErrorValidacion> errores)
    {
        if (guia.FechaInicioTraslado < guia.FechaEmision)
            errores.Add(ErrorValidacion.De(R.FechaInicioAnteriorAEmision, "fechaInicioTraslado",
                "La fecha de inicio del traslado no puede ser anterior a la fecha de emisión."));

        if (guia.PesoBrutoTotal <= 0)
            errores.Add(ErrorValidacion.De(R.PesoBrutoNoPositivo, "pesoBrutoTotal", "El peso bruto total debe ser mayor que cero."));

        if (guia.UnidadPeso != UnidadesPeso.Kilogramos)
            errores.Add(ErrorValidacion.De(R.UnidadPesoInvalida, "unidadPeso",
                $"La unidad del peso bruto total debe ser {UnidadesPeso.Kilogramos}."));

        ValidarPunto(guia.Partida, "partida", errores);
        ValidarPunto(guia.Llegada, "llegada", errores);
    }

    private static void ValidarPunto(PuntoTraslado punto, string campo, List<ErrorValidacion> errores)
    {
        if (punto.Ubigeo is null || punto.Ubigeo.Length != 6 || !punto.Ubigeo.All(char.IsAsciiDigit))
            errores.Add(ErrorValidacion.De(R.UbigeoInvalido, $"{campo}.ubigeo", "El ubigeo debe tener 6 dígitos."));

        if (string.IsNullOrWhiteSpace(punto.Direccion))
            errores.Add(ErrorValidacion.De(R.DireccionObligatoria, $"{campo}.direccion", "La dirección es obligatoria."));
    }

    private static void ValidarBienes(IReadOnlyList<Bien>? bienes, List<ErrorValidacion> errores)
    {
        if (bienes is null || bienes.Count == 0)
        {
            errores.Add(ErrorValidacion.De(R.SinBienes, "bienes", "La guía debe incluir al menos un bien."));
            return;
        }

        for (var i = 0; i < bienes.Count; i++)
        {
            var bien = bienes[i];
            var campo = $"bienes[{i}]";

            if (string.IsNullOrWhiteSpace(bien.Descripcion) || bien.Descripcion.Length > MaxDescripcionBien)
                errores.Add(ErrorValidacion.De(R.DescripcionBienInvalida, $"{campo}.descripcion",
                    $"La descripción es obligatoria y admite hasta {MaxDescripcionBien} caracteres."));

            if (bien.Cantidad <= 0)
                errores.Add(ErrorValidacion.De(R.CantidadBienNoPositiva, $"{campo}.cantidad", "La cantidad debe ser mayor que cero."));

            if (string.IsNullOrWhiteSpace(bien.UnidadMedida))
                errores.Add(ErrorValidacion.De(R.UnidadMedidaBienObligatoria, $"{campo}.unidadMedida",
                    "La unidad de medida es obligatoria."));
        }
    }

    private static void ValidarTransporte(GuiaRemision guia, List<ErrorValidacion> errores)
    {
        var transporte = guia.Transporte;

        if (transporte.Modalidad == ModalidadTransporte.Publico)
        {
            var transportista = transporte.Transportista;
            if (transportista is null)
            {
                errores.Add(ErrorValidacion.De(R.TransportistaObligatorio, "transporte.transportista",
                    "En transporte público se debe indicar el transportista."));
                return;
            }

            if (!DocumentoIdentidad.EsRucValido(transportista.Ruc))
                errores.Add(ErrorValidacion.De(R.RucTransportistaInvalido, "transporte.transportista.ruc",
                    "El RUC del transportista no es válido."));
            else if (transportista.Ruc == guia.Remitente.Ruc)
                // Si el remitente traslada con sus propios medios, es transporte privado.
                errores.Add(ErrorValidacion.De(R.TransportistaIgualARemitente, "transporte.transportista.ruc",
                    "El transportista no puede ser el remitente: en ese caso el transporte es privado."));
            return;
        }

        if (transporte.Vehiculos.Count == 0)
            errores.Add(ErrorValidacion.De(R.SinVehiculos, "transporte.vehiculos",
                "En transporte privado se debe indicar al menos un vehículo."));

        for (var i = 0; i < transporte.Vehiculos.Count; i++)
        {
            if (transporte.Vehiculos[i] is not { } placa || !Placa().IsMatch(placa))
                errores.Add(ErrorValidacion.De(R.PlacaInvalida, $"transporte.vehiculos[{i}]",
                    "La placa debe tener de 6 a 8 letras mayúsculas o números, sin guion."));
        }

        if (transporte.Conductores.Count == 0)
            errores.Add(ErrorValidacion.De(R.SinConductores, "transporte.conductores",
                "En transporte privado se debe indicar al menos un conductor."));

        for (var i = 0; i < transporte.Conductores.Count; i++)
        {
            var conductor = transporte.Conductores[i];
            var campo = $"transporte.conductores[{i}]";

            ValidarDocumento(conductor.TipoDocumento, conductor.NumeroDocumento, campo,
                R.DocumentoConductorInvalido, R.DocumentoConductorInvalido, errores);

            if (string.IsNullOrWhiteSpace(conductor.Nombres))
                errores.Add(ErrorValidacion.De(R.NombresConductorObligatorios, $"{campo}.nombres", "Los nombres del conductor son obligatorios."));

            if (string.IsNullOrWhiteSpace(conductor.Apellidos))
                errores.Add(ErrorValidacion.De(R.ApellidosConductorObligatorios, $"{campo}.apellidos", "Los apellidos del conductor son obligatorios."));

            if (string.IsNullOrWhiteSpace(conductor.Licencia))
                errores.Add(ErrorValidacion.De(R.LicenciaConductorObligatoria, $"{campo}.licencia", "La licencia de conducir es obligatoria."));
        }
    }

    /// <summary>Tipo del catálogo 06 y número con el formato que corresponde a ese tipo.</summary>
    private static void ValidarDocumento(string tipo, string numero, string campo, Regla reglaTipo, Regla reglaNumero,
        List<ErrorValidacion> errores)
    {
        bool? numeroValido = tipo switch
        {
            TiposDocumento.Ruc => DocumentoIdentidad.EsRucValido(numero),
            TiposDocumento.Dni => DocumentoIdentidad.EsDniValido(numero),
            TiposDocumento.CarneExtranjeria or TiposDocumento.Pasaporte => DocumentoIdentidad.EsAlfanumericoValido(numero),
            _ => null,
        };

        if (numeroValido is null)
            errores.Add(ErrorValidacion.De(reglaTipo, $"{campo}.tipoDocumento",
                "Tipo de documento no válido. Use 6 (RUC), 1 (DNI), 4 (carné de extranjería) o 7 (pasaporte)."));
        else if (numeroValido == false)
            errores.Add(ErrorValidacion.De(reglaNumero, $"{campo}.numeroDocumento",
                "El número de documento no tiene el formato que corresponde a su tipo."));
    }

    [GeneratedRegex("^T[A-Z0-9]{3}$")]
    private static partial Regex SerieGre();

    [GeneratedRegex("^[A-Z0-9]{6,8}$")]
    private static partial Regex Placa();
}
