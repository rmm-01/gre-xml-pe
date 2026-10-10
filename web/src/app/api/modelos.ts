// Contrato de la API (src/GreXml.Api). Cada tipo refleja un record de C#: si la API cambia, esto debe cambiar.

export type Modalidad = 'Publico' | 'Privado';

/** Catálogo 20 de SUNAT: solo los motivos que la API soporta. */
export const MOTIVOS = [
  { codigo: '01', nombre: 'Venta' },
  { codigo: '02', nombre: 'Compra' },
  { codigo: '04', nombre: 'Traslado entre establecimientos' },
] as const;

export type CodigoMotivo = (typeof MOTIVOS)[number]['codigo'];

export interface Contribuyente {
  ruc: string;
  razonSocial: string;
}

export interface Destinatario {
  tipoDocumento: string;
  numeroDocumento: string;
  nombre: string;
}

export interface PuntoTraslado {
  ubigeo: string;
  direccion: string;
}

export interface Bien {
  descripcion: string;
  cantidad: number;
  unidadMedida: string;
  codigo?: string | null;
}

export interface Conductor {
  tipoDocumento: string;
  numeroDocumento: string;
  nombres: string;
  apellidos: string;
  licencia: string;
}

export interface Transporte {
  modalidad: Modalidad;
  transportista?: Contribuyente | null;
  vehiculos: string[];
  conductores: Conductor[];
}

export interface GuiaRemision {
  serie: string;
  numero: number;
  /** yyyy-MM-dd */
  fechaEmision: string;
  /** HH:mm:ss */
  horaEmision: string;
  remitente: Contribuyente;
  destinatario: Destinatario;
  /** Solo en el motivo 02 (compra). */
  proveedor?: Contribuyente | null;
  motivoTraslado: string;
  fechaInicioTraslado: string;
  pesoBrutoTotal: number;
  unidadPeso: string;
  partida: PuntoTraslado;
  llegada: PuntoTraslado;
  bienes: Bien[];
  transporte: Transporte;
  observaciones?: string | null;
}

/** Regla incumplida. `campo` es la ruta en el JSON de la guía, por ejemplo "bienes[1].cantidad". */
export interface ErrorValidacion {
  codigo: string;
  codigoSunat: string | null;
  campo: string;
  mensaje: string;
}

export interface RespuestaValidacion {
  valida: boolean;
  errores: ErrorValidacion[];
}

export interface ErrorXsd {
  mensaje: string;
  linea: number;
  columna: number;
}

export interface RespuestaGeneracion {
  nombreArchivo: string;
  xmlFirmado: string;
  cumpleEsquema: boolean;
  erroresEsquema: ErrorXsd[];
  firmaValida: boolean;
  zipBase64: string;
  hashZip: string;
}

export interface Observacion {
  codigo: string;
  descripcion: string;
}

export interface RespuestaCdr {
  estado: 'Aceptada' | 'Observada' | 'Rechazada';
  codigo: string;
  descripcion: string;
  observaciones: Observacion[];
}

export interface ErrorSunat {
  numError: string;
  desError: string;
}

/** codRespuesta: "98" en proceso, "0" terminado con CDR, "99" terminado con error. */
export interface RespuestaEnvio {
  ticket: string;
  codRespuesta: string;
  cdr: RespuestaCdr | null;
  error: ErrorSunat | null;
}

/**
 * Lo que puede pasar al generar o enviar: éxito, guía con errores de negocio (422 con la lista)
 * o archivo rechazado por SUNAT simulado antes de dar ticket (422 con código, por ejemplo 2223).
 */
export type ResultadoApi<T> =
  | { tipo: 'ok'; datos: T }
  | { tipo: 'invalida'; errores: ErrorValidacion[] }
  | { tipo: 'rechazada'; codigo: string; detalle: string };
