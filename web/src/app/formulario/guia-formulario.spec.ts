import { TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';
import { guiaDeCompra, guiaDeVenta } from '../../testing/guia-ejemplo';
import {
  aGuia,
  cargarGuia,
  crearFormulario,
  fechaLocal,
  FormularioGuia,
  nuevoBien,
} from './guia-formulario';

describe('guia-formulario', () => {
  let fb: NonNullableFormBuilder;
  let form: FormularioGuia;

  beforeEach(() => {
    fb = TestBed.inject(NonNullableFormBuilder);
    form = crearFormulario(fb, new Date(2026, 9, 10, 8, 5, 7));
  });

  it('devuelve la misma guía que se cargó', () => {
    cargarGuia(fb, form, guiaDeVenta());

    expect(aGuia(form)).toEqual(guiaDeVenta());
  });

  it('ajusta las listas al tamaño de la guía cargada', () => {
    form.controls.bienes.push(nuevoBien(fb));
    form.controls.bienes.push(nuevoBien(fb));

    cargarGuia(fb, form, guiaDeVenta());

    expect(form.controls.bienes.length).toBe(2);
    expect(form.controls.transporte.controls.vehiculos.length).toBe(1);
  });

  it('envía el proveedor solo en el motivo 02', () => {
    cargarGuia(fb, form, guiaDeCompra());
    expect(aGuia(form).proveedor).toEqual({
      ruc: '20100000084',
      razonSocial: 'FABRICA INVENTADA SA',
    });

    form.controls.motivoTraslado.setValue('04');
    expect(aGuia(form).proveedor).toBeNull();

    // Volver al motivo 02 recupera lo escrito: el formulario no lo borró.
    form.controls.motivoTraslado.setValue('02');
    expect(aGuia(form).proveedor?.ruc).toBe('20100000084');
  });

  it('en transporte público envía el transportista y no los vehículos ni conductores', () => {
    cargarGuia(fb, form, guiaDeVenta());
    const transporte = form.controls.transporte.controls;
    transporte.modalidad.setValue('Publico');
    transporte.transportista.setValue({
      ruc: '20100000076',
      razonSocial: 'TRANSPORTES INVENTADOS SAC',
    });

    expect(aGuia(form).transporte).toEqual({
      modalidad: 'Publico',
      transportista: { ruc: '20100000076', razonSocial: 'TRANSPORTES INVENTADOS SAC' },
      vehiculos: [],
      conductores: [],
    });
  });

  it('en transporte privado no envía el transportista', () => {
    cargarGuia(fb, form, guiaDeVenta());
    form.controls.transporte.controls.transportista.setValue({
      ruc: '20100000076',
      razonSocial: 'X',
    });

    expect(aGuia(form).transporte.transportista).toBeNull();
  });

  it('empieza con la fecha y la hora indicadas', () => {
    const guia = aGuia(form);

    expect(guia.fechaEmision).toBe('2026-10-10');
    expect(guia.fechaInicioTraslado).toBe('2026-10-10');
    expect(guia.horaEmision).toBe('08:05:07');
  });

  it('completa los segundos de la hora y convierte los vacíos', () => {
    form.controls.horaEmision.setValue('14:30');

    const guia = aGuia(form);

    expect(guia.horaEmision).toBe('14:30:00');
    expect(guia.numero).toBe(0);
    expect(guia.pesoBrutoTotal).toBe(0);
    expect(guia.bienes[0]).toEqual({
      descripcion: '',
      cantidad: 0,
      unidadMedida: 'NIU',
      codigo: null,
    });
    expect(guia.observaciones).toBeNull();
  });

  it('usa la fecha local, no la de UTC', () => {
    // 10 de octubre a las 23:30 en Lima ya es 11 de octubre en UTC.
    expect(fechaLocal(new Date(2026, 9, 10, 23, 30))).toBe('2026-10-10');
  });
});
