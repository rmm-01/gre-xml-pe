import { FormArray, FormControl, FormGroup, NonNullableFormBuilder } from '@angular/forms';
import { Bien, CodigoMotivo, Contribuyente, GuiaRemision, Modalidad } from '../api/modelos';

// El formulario guarda solo texto y números; la conversión a GuiaRemision decide qué se envía según el motivo
// y la modalidad. Así, cambiar de motivo y volver no borra lo que el usuario ya escribió.

export const MOTIVO_CON_PROVEEDOR: CodigoMotivo = '02';

const contribuyente = (fb: NonNullableFormBuilder) => fb.group({ ruc: '', razonSocial: '' });

const puntoTraslado = (fb: NonNullableFormBuilder) => fb.group({ ubigeo: '', direccion: '' });

export const nuevoBien = (fb: NonNullableFormBuilder) =>
  fb.group({
    descripcion: '',
    cantidad: fb.control<number | null>(null),
    unidadMedida: 'NIU',
    codigo: '',
  });

export const nuevoConductor = (fb: NonNullableFormBuilder) =>
  fb.group({ tipoDocumento: '1', numeroDocumento: '', nombres: '', apellidos: '', licencia: '' });

export const nuevoVehiculo = (fb: NonNullableFormBuilder) => fb.control('');

/** Formulario vacío con la fecha y la hora de `ahora`: sin hora, la API no puede leer la guía y responde 400. */
export function crearFormulario(fb: NonNullableFormBuilder, ahora = new Date()) {
  const hoy = fechaLocal(ahora);
  return fb.group({
    serie: '',
    numero: fb.control<number | null>(null),
    fechaEmision: hoy,
    horaEmision: horaLocal(ahora),
    motivoTraslado: fb.control<CodigoMotivo>('01'),
    remitente: contribuyente(fb),
    destinatario: fb.group({ tipoDocumento: '6', numeroDocumento: '', nombre: '' }),
    proveedor: contribuyente(fb),
    fechaInicioTraslado: hoy,
    pesoBrutoTotal: fb.control<number | null>(null),
    unidadPeso: 'KGM',
    partida: puntoTraslado(fb),
    llegada: puntoTraslado(fb),
    bienes: fb.array([nuevoBien(fb)]),
    transporte: fb.group({
      modalidad: fb.control<Modalidad>('Privado'),
      transportista: contribuyente(fb),
      vehiculos: fb.array([nuevoVehiculo(fb)]),
      conductores: fb.array([nuevoConductor(fb)]),
    }),
    observaciones: '',
  });
}

export type FormularioGuia = ReturnType<typeof crearFormulario>;

/** Copia una guía al formulario. Las listas se ajustan al tamaño de la guía antes de copiar los valores. */
export function cargarGuia(
  fb: NonNullableFormBuilder,
  form: FormularioGuia,
  guia: GuiaRemision,
): void {
  const { bienes, transporte } = form.controls;
  ajustar(bienes, guia.bienes.length, () => nuevoBien(fb));
  ajustar(transporte.controls.vehiculos, guia.transporte.vehiculos.length, () => nuevoVehiculo(fb));
  ajustar(transporte.controls.conductores, guia.transporte.conductores.length, () =>
    nuevoConductor(fb),
  );

  const vacio: Contribuyente = { ruc: '', razonSocial: '' };
  form.reset();
  form.patchValue({
    ...guia,
    motivoTraslado: guia.motivoTraslado as CodigoMotivo,
    proveedor: guia.proveedor ?? vacio,
    observaciones: guia.observaciones ?? '',
    bienes: guia.bienes.map((b) => ({ ...b, codigo: b.codigo ?? '' })),
    transporte: { ...guia.transporte, transportista: guia.transporte.transportista ?? vacio },
  });
}

/**
 * Arma la guía que se envía a la API. Solo incluye el proveedor en el motivo 02 y, según la modalidad,
 * el transportista o los vehículos y conductores: lo demás queda en el formulario, pero no se envía.
 */
export function aGuia(form: FormularioGuia): GuiaRemision {
  const v = form.getRawValue();
  const publico = v.transporte.modalidad === 'Publico';

  return {
    ...v,
    // Un número vacío se envía como 0 para que la API responda con el error del campo y no con un 400.
    numero: v.numero ?? 0,
    horaEmision: conSegundos(v.horaEmision),
    proveedor: v.motivoTraslado === MOTIVO_CON_PROVEEDOR ? v.proveedor : null,
    pesoBrutoTotal: v.pesoBrutoTotal ?? 0,
    bienes: v.bienes.map((b): Bien => ({
      ...b,
      cantidad: b.cantidad ?? 0,
      codigo: b.codigo.trim() || null,
    })),
    transporte: {
      modalidad: v.transporte.modalidad,
      transportista: publico ? v.transporte.transportista : null,
      vehiculos: publico ? [] : v.transporte.vehiculos,
      conductores: publico ? [] : v.transporte.conductores,
    },
    observaciones: v.observaciones.trim() || null,
  };
}

/** Fecha local en formato yyyy-MM-dd (toISOString usaría UTC y en Perú daría el día siguiente desde las 19:00). */
export function fechaLocal(fecha = new Date()): string {
  return `${fecha.getFullYear()}-${dos(fecha.getMonth() + 1)}-${dos(fecha.getDate())}`;
}

/** Hora local en formato HH:mm:ss. */
export function horaLocal(fecha = new Date()): string {
  return `${dos(fecha.getHours())}:${dos(fecha.getMinutes())}:${dos(fecha.getSeconds())}`;
}

/** `<input type="time">` devuelve "HH:mm"; la API espera "HH:mm:ss". */
function conSegundos(hora: string): string {
  return /^\d{2}:\d{2}$/.test(hora) ? `${hora}:00` : hora;
}

function dos(n: number): string {
  return String(n).padStart(2, '0');
}

function ajustar<T extends FormGroup | FormControl>(
  lista: FormArray<T>,
  tamano: number,
  crear: () => T,
): void {
  while (lista.length > tamano) lista.removeAt(lista.length - 1);
  while (lista.length < tamano) lista.push(crear());
}
