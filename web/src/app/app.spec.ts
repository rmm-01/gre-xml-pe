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

    // El mismo error aparece junto al campo, y se va cuando el usuario corrige la serie.
    expect(serie.closest('label')?.querySelector('.error-campo')?.textContent).toContain('GRE-101');
    expect(serie.classList).toContain('ng-invalid');
    serie.value = 'T001';
    serie.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(serie.closest('label')?.querySelector('.error-campo')).toBeNull();
  });

  it('marca el error en la fila del bien y arriba de la lista de vehículos', async () => {
    const { fixture, elemento, boton, seccion } = await crear();
    boton('Cargar ejemplo').click();
    http.expectOne((r) => r.url === '/guias/ejemplo').flush(guiaDeVenta());
    await fixture.whenStable();

    boton('Generar XML').click();
    http.expectOne('/guias/generar').flush(
      {
        valida: false,
        errores: [
          {
            codigo: 'GRE-208',
            codigoSunat: null,
            campo: 'bienes[1].cantidad',
            mensaje: 'Cantidad',
          },
          {
            codigo: 'GRE-304',
            codigoSunat: '1067',
            campo: 'transporte.vehiculos',
            mensaje: 'Sin vehículos',
          },
        ],
      },
      { status: 422, statusText: 'Unprocessable Entity' },
    );
    await fixture.whenStable();

    const filas = seccion('bienes')!.querySelectorAll('.fila');
    expect(filas[0].querySelector('.error-campo')).toBeNull();
    expect(filas[1].querySelector('.error-campo')?.textContent).toContain('GRE-208');
    // El error de una lista completa va arriba de la sección, no dentro de una fila.
    const errorDeSeccion = seccion('vehiculos')!.querySelector(':scope > app-error-campo');
    expect(errorDeSeccion?.textContent).toContain('GRE-304');
    expect(seccion('vehiculos')!.querySelector('.fila .error-campo')).toBeNull();
    expect(elemento.querySelector('[data-panel=generacion]')).toBeNull();
  });

  it('muestra el XML generado y sus comprobaciones', async () => {
    const { fixture, elemento, boton } = await crear();

    boton('Generar XML').click();
    http.expectOne('/guias/generar').flush({
      nombreArchivo: '20100000050-09-T001-1.zip',
      xmlFirmado: '<DespatchAdvice>firmado</DespatchAdvice>',
      cumpleEsquema: true,
      erroresEsquema: [],
      firmaValida: true,
      zipBase64: 'UEsDBA==',
      hashZip: 'abc123',
    });
    await fixture.whenStable();

    const panel = elemento.querySelector('[data-panel=generacion]')!;
    expect(panel.querySelector('.xml')?.textContent).toContain(
      '<DespatchAdvice>firmado</DespatchAdvice>',
    );
    expect(panel.textContent).toContain('Cumple el esquema');
    expect(panel.textContent).toContain('Firma válida');
    expect(panel.textContent).toContain('Descargar 20100000050-09-T001-1.zip');
    expect(elemento.querySelector('.ok')?.textContent).toContain('cumple todas las reglas');
  });

  it('muestra el CDR observado con sus observaciones', async () => {
    const { fixture, elemento, boton } = await crear();

    boton('Enviar a SUNAT (simulado)').click();
    http.expectOne('/guias/enviar').flush({
      ticket: 'T-1',
      codRespuesta: '0',
      cdr: {
        estado: 'Observada',
        codigo: '0',
        descripcion: 'La guía ha sido aceptada',
        observaciones: [{ codigo: '4398', descripcion: 'Placa no registrada' }],
      },
      error: null,
    });
    await fixture.whenStable();

    const cdr = elemento.querySelector('[data-panel=envio] .cdr')!;
    expect(cdr.classList).toContain('observada');
    expect(cdr.textContent).toContain('CDR: Observada');
    expect(cdr.querySelector('li')?.textContent).toContain('4398');
  });

  it('consulta otra vez el ticket que sigue en proceso', async () => {
    const { fixture, elemento, boton } = await crear();

    boton('Enviar a SUNAT (simulado)').click();
    http
      .expectOne('/guias/enviar')
      .flush({ ticket: 'T/2', codRespuesta: '98', cdr: null, error: null });
    await fixture.whenStable();
    expect(elemento.querySelector('[data-panel=envio]')?.textContent).toContain('En proceso');

    boton('Consultar estado').click();
    http.expectOne('/envios/T%2F2').flush({
      ticket: 'T/2',
      codRespuesta: '0',
      cdr: { estado: 'Aceptada', codigo: '0', descripcion: 'Aceptada', observaciones: [] },
      error: null,
    });
    await fixture.whenStable();

    expect(elemento.querySelector('[data-panel=envio]')?.textContent).not.toContain('En proceso');
    expect(elemento.querySelector('.cdr')?.textContent).toContain('CDR: Aceptada');
  });

  it('muestra el error de SUNAT cuando el envío termina sin CDR', async () => {
    const { fixture, elemento, boton } = await crear();

    boton('Enviar a SUNAT (simulado)').click();
    http.expectOne('/guias/enviar').flush({
      ticket: 'T-3',
      codRespuesta: '99',
      cdr: null,
      error: { numError: '1034', desError: 'El RUC del nombre del archivo no coincide' },
    });
    await fixture.whenStable();

    expect(elemento.querySelector('[data-panel=envio]')?.textContent).toContain('1034');
  });

  it('muestra el rechazo de SUNAT simulado antes de dar ticket', async () => {
    const { fixture, elemento, boton } = await crear();

    boton('Enviar a SUNAT (simulado)').click();
    http.expectOne('/guias/enviar').flush(
      {
        title: 'SUNAT (simulado) no aceptó el archivo',
        detail: 'Ya fue aceptada',
        codigo: '2223',
      },
      { status: 422, statusText: 'Unprocessable Entity' },
    );
    await fixture.whenStable();

    const panel = elemento.querySelector('[data-panel=rechazo]')?.textContent;
    expect(panel).toContain('2223');
    expect(panel).toContain('Ya fue aceptada');
    expect(elemento.querySelector('[data-panel=envio]')).toBeNull();
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
