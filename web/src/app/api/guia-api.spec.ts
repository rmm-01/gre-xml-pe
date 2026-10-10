import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { GuiaApi } from './guia-api';
import { GuiaRemision, ResultadoApi } from './modelos';

describe('GuiaApi', () => {
  let api: GuiaApi;
  let http: HttpTestingController;
  const guia = { serie: 'T001', numero: 1 } as GuiaRemision;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(GuiaApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('pide el ejemplo del motivo indicado', async () => {
    const respuesta = firstValueFrom(api.ejemplo('02'));

    const peticion = http.expectOne((r) => r.url === '/guias/ejemplo');
    expect(peticion.request.params.get('motivo')).toBe('02');
    peticion.flush(guia);

    expect(await respuesta).toEqual(guia);
  });

  it('generar: 200 se convierte en resultado ok', async () => {
    const respuesta = firstValueFrom(api.generar(guia));

    http.expectOne('/guias/generar').flush({ nombreArchivo: 'x.zip' });

    expect(await respuesta).toEqual({ tipo: 'ok', datos: { nombreArchivo: 'x.zip' } });
  });

  it('generar: 422 con lista de errores se convierte en resultado invalida', async () => {
    const errores = [{ codigo: 'GRE-101', codigoSunat: '1001', campo: 'serie', mensaje: 'Serie inválida' }];
    const respuesta = firstValueFrom(api.generar(guia));

    http.expectOne('/guias/generar').flush({ valida: false, errores }, { status: 422, statusText: 'Unprocessable Entity' });

    expect(await respuesta).toEqual({ tipo: 'invalida', errores });
  });

  it('enviar: 422 con código de SUNAT se convierte en resultado rechazada', async () => {
    const respuesta = firstValueFrom(api.enviar(guia));

    http
      .expectOne('/guias/enviar')
      .flush({ title: 'x', codigo: '2223', detail: '2223 - El archivo ya fue presentado anteriormente' }, { status: 422, statusText: 'x' });

    const esperado: ResultadoApi<unknown> = {
      tipo: 'rechazada',
      codigo: '2223',
      detalle: '2223 - El archivo ya fue presentado anteriormente',
    };
    expect(await respuesta).toEqual(esperado);
  });

  it('enviar: un 500 sigue siendo un error', async () => {
    const respuesta = firstValueFrom(api.enviar(guia));

    http.expectOne('/guias/enviar').flush('falla', { status: 500, statusText: 'Server Error' });

    await expect(respuesta).rejects.toMatchObject({ status: 500 });
  });

  it('consultarEnvio escapa el ticket en la URL', () => {
    api.consultarEnvio('a/b').subscribe();

    http.expectOne('/envios/a%2Fb').flush({});
  });
});
