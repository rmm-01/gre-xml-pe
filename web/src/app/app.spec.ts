import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { guiaDeCompra, guiaDeVenta } from '../testing/guia-ejemplo';
import { App } from './app';

describe('App', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function crear() {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const elemento = fixture.nativeElement as HTMLElement;
    const boton = (texto: string) =>
      Array.from(elemento.querySelectorAll('button')).find((b) => b.textContent?.trim() === texto)!;
    const seccion = (nombre: string) => elemento.querySelector(`[data-seccion=${nombre}]`);
    return { fixture, elemento, boton, seccion };
  }

  function elegirMotivo(elemento: HTMLElement, motivo: string) {
    const select = elemento.querySelector<HTMLSelectElement>('select[name=motivo]')!;
    select.value = motivo;
    select.dispatchEvent(new Event('change'));
  }

  it('muestra el título', async () => {
    const { elemento } = await crear();

    expect(elemento.querySelector('h1')?.textContent).toContain('Guía de Remisión Electrónica');
  });

  it('pide el ejemplo del motivo elegido y llena el formulario', async () => {
    const { fixture, elemento, boton, seccion } = await crear();
    elegirMotivo(elemento, '02');

    boton('Cargar ejemplo').click();
    const peticion = http.expectOne((r) => r.url === '/guias/ejemplo');
    expect(peticion.request.params.get('motivo')).toBe('02');
    peticion.flush(guiaDeCompra());
    await fixture.whenStable();

    expect(seccion('proveedor')?.querySelector('input')?.value).toBe('20100000084');
    expect(seccion('bienes')?.querySelectorAll('.fila').length).toBe(2);
  });

  it('valida lo que hay en el formulario y muestra los errores de la API', async () => {
    const { fixture, elemento, boton } = await crear();
    boton('Cargar ejemplo').click();
    http.expectOne((r) => r.url === '/guias/ejemplo').flush(guiaDeVenta());
    await fixture.whenStable();

    const serie = elemento.querySelector<HTMLInputElement>('input[formcontrolname=serie]')!;
    serie.value = 'X1';
    serie.dispatchEvent(new Event('input'));

    boton('Validar').click();
    const peticion = http.expectOne('/guias/validar');
    expect(peticion.request.body.serie).toBe('X1');
    peticion.flush({
      valida: false,
      errores: [
        { codigo: 'GRE-101', codigoSunat: '1001', campo: 'serie', mensaje: 'Serie inválida' },
      ],
    });
    await fixture.whenStable();

    const error = elemento.querySelector('.errores li')?.textContent;
    expect(error).toContain('serie');
    expect(error).toContain('SUNAT 1001');
  });

  it('muestra el proveedor solo con el motivo 02', async () => {
    const { fixture, elemento, seccion } = await crear();
    expect(seccion('proveedor')).toBeNull();

    elegirMotivo(elemento, '02');
    await fixture.whenStable();
    expect(seccion('proveedor')).not.toBeNull();

    elegirMotivo(elemento, '04');
    await fixture.whenStable();
    expect(seccion('proveedor')).toBeNull();
  });

  it('pide el transportista en transporte público y los vehículos y conductores en privado', async () => {
    const { fixture, elemento, seccion } = await crear();
    expect(seccion('vehiculos')).not.toBeNull();
    expect(seccion('transportista')).toBeNull();

    elemento.querySelector<HTMLInputElement>('input[type=radio][value=Publico]')!.click();
    await fixture.whenStable();

    expect(seccion('transportista')).not.toBeNull();
    expect(seccion('vehiculos')).toBeNull();
    expect(seccion('conductores')).toBeNull();
  });

  it('agrega y quita bienes, pero deja al menos uno', async () => {
    const { fixture, boton, seccion } = await crear();
    const filas = () => seccion('bienes')!.querySelectorAll('.fila');
    const quitar = () => seccion('bienes')!.querySelector<HTMLButtonElement>('.quitar')!;
    expect(quitar().disabled).toBe(true);

    boton('Agregar bien').click();
    await fixture.whenStable();
    expect(filas().length).toBe(2);
    expect(quitar().disabled).toBe(false);

    quitar().click();
    await fixture.whenStable();
    expect(filas().length).toBe(1);
  });

  it('avisa si la API no responde', async () => {
    const { fixture, elemento, boton } = await crear();

    boton('Cargar ejemplo').click();
    http.expectOne((r) => r.url === '/guias/ejemplo').error(new ProgressEvent('error'));
    await fixture.whenStable();

    expect(elemento.querySelector('[role=alert]')?.textContent).toContain('localhost:5142');
  });

  it('avisa si la API no puede leer la guía', async () => {
    const { fixture, elemento, boton } = await crear();

    boton('Validar').click();
    http.expectOne('/guias/validar').flush(null, { status: 400, statusText: 'Bad Request' });
    await fixture.whenStable();

    expect(elemento.querySelector('[role=alert]')?.textContent).toContain('no pudo leer la guía');
  });
});
