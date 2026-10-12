import { TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';
import { guiaDeVenta } from '../../testing/guia-ejemplo';
import { ErrorValidacion } from '../api/modelos';
import { erroresApi, marcarErrores, rutaFormulario } from './errores';
import { cargarGuia, crearFormulario, FormularioGuia } from './guia-formulario';

const error = (campo: string, codigo = 'GRE-000'): ErrorValidacion => ({
  codigo,
  codigoSunat: null,
  campo,
  mensaje: `Error en ${campo}`,
});

describe('rutaFormulario', () => {
  it.each([
    ['serie', 'serie'],
    ['remitente.ruc', 'remitente.ruc'],
    ['bienes[1].cantidad', 'bienes.1.cantidad'],
    ['transporte.vehiculos[0]', 'transporte.vehiculos.0'],
    ['transporte.conductores[12].licencia', 'transporte.conductores.12.licencia'],
  ])('convierte "%s" en "%s"', (campo, ruta) => {
    expect(rutaFormulario(campo)).toBe(ruta);
  });
});

describe('marcarErrores', () => {
  let form: FormularioGuia;

  beforeEach(() => {
    const fb = TestBed.inject(NonNullableFormBuilder);
    form = crearFormulario(fb);
    cargarGuia(fb, form, guiaDeVenta());
  });

  it('guarda cada error en el control de su campo, también dentro de las listas', () => {
    marcarErrores(form, [error('bienes[1].cantidad'), error('transporte.vehiculos[0]')]);

    expect(erroresApi(form.get('bienes.1.cantidad'))).toEqual([error('bienes[1].cantidad')]);
    expect(erroresApi(form.get('transporte.vehiculos.0'))).toEqual([
      error('transporte.vehiculos[0]'),
    ]);
    expect(form.get('bienes.0.cantidad')?.valid).toBe(true);
  });

  it('marca una sección completa y junta los errores del mismo campo', () => {
    marcarErrores(form, [
      error('bienes', 'GRE-206'),
      error('serie', 'GRE-101'),
      error('serie', 'GRE-999'),
    ]);

    expect(erroresApi(form.controls.bienes).map((e) => e.codigo)).toEqual(['GRE-206']);
    expect(erroresApi(form.controls.serie).map((e) => e.codigo)).toEqual(['GRE-101', 'GRE-999']);
  });

  it('ignora los campos que no están en el formulario', () => {
    expect(() =>
      marcarErrores(form, [error('campoInexistente'), error('bienes[9].cantidad')]),
    ).not.toThrow();
    expect(form.valid).toBe(true);
  });

  it('borra los errores de la validación anterior', () => {
    marcarErrores(form, [error('serie'), error('bienes[0].descripcion')]);

    marcarErrores(form, [error('numero')]);

    expect(erroresApi(form.controls.serie)).toEqual([]);
    expect(erroresApi(form.get('bienes.0.descripcion'))).toEqual([]);
    expect(erroresApi(form.controls.numero)).toHaveLength(1);
  });

  it('el error desaparece cuando el usuario cambia el campo', () => {
    marcarErrores(form, [error('serie')]);

    form.controls.serie.setValue('T002');

    expect(form.controls.serie.errors).toBeNull();
  });
});
