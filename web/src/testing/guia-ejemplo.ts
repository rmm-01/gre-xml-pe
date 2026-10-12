import { GuiaRemision } from '../app/api/modelos';

/** Misma guía de venta que devuelve GET /guias/ejemplo?motivo=01, con datos inventados. Solo para pruebas. */
export function guiaDeVenta(): GuiaRemision {
  return {
    serie: 'T001',
    numero: 1,
    fechaEmision: '2026-10-10',
    horaEmision: '09:00:00',
    remitente: { ruc: '20100000050', razonSocial: 'DISTRIBUIDORA EJEMPLO SAC' },
    destinatario: {
      tipoDocumento: '6',
      numeroDocumento: '20100000068',
      nombre: 'COMERCIAL DE PRUEBA EIRL',
    },
    proveedor: null,
    motivoTraslado: '01',
    fechaInicioTraslado: '2026-10-10',
    pesoBrutoTotal: 1250.5,
    unidadPeso: 'KGM',
    partida: { ubigeo: '150101', direccion: 'AV. LOS EJEMPLOS 123, LIMA' },
    llegada: { ubigeo: '040101', direccion: 'CALLE FICTICIA 456, AREQUIPA' },
    bienes: [
      {
        descripcion: 'CEMENTO PORTLAND TIPO I BOLSA 42.5 KG',
        cantidad: 20,
        unidadMedida: 'NIU',
        codigo: 'CEM-001',
      },
      {
        descripcion: 'FIERRO CORRUGADO 1/2 PULGADA',
        cantidad: 500,
        unidadMedida: 'KGM',
        codigo: null,
      },
    ],
    transporte: {
      modalidad: 'Privado',
      transportista: null,
      vehiculos: ['ABC123'],
      conductores: [
        {
          tipoDocumento: '1',
          numeroDocumento: '12345678',
          nombres: 'JUAN CARLOS',
          apellidos: 'PEREZ GOMEZ',
          licencia: 'Q12345678',
        },
      ],
    },
    observaciones: null,
  };
}

export function guiaDeCompra(): GuiaRemision {
  return {
    ...guiaDeVenta(),
    motivoTraslado: '02',
    destinatario: {
      tipoDocumento: '6',
      numeroDocumento: '20100000050',
      nombre: 'DISTRIBUIDORA EJEMPLO SAC',
    },
    proveedor: { ruc: '20100000084', razonSocial: 'FABRICA INVENTADA SA' },
  };
}
