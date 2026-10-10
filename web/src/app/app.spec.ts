import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
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

  function botones(elemento: HTMLElement): HTMLButtonElement[] {
    return Array.from(elemento.querySelectorAll('button'));
  }

  it('muestra el título', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('h1')?.textContent).toContain('Guía de Remisión Electrónica');
  });

  it('carga el ejemplo y muestra los errores que devuelve la API al validar', async () => {
    const fixture = TestBed.createComponent(App);
    const elemento = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();

    botones(elemento)[0].click();
    http.expectOne((r) => r.url === '/guias/ejemplo').flush({ serie: 'T001' });
    await fixture.whenStable();

    botones(elemento)[1].click();
    http.expectOne('/guias/validar').flush({
      valida: false,
      errores: [{ codigo: 'GRE-101', codigoSunat: '1001', campo: 'serie', mensaje: 'Serie inválida' }],
    });
    await fixture.whenStable();

    const error = elemento.querySelector('.errores li')?.textContent;
    expect(error).toContain('serie');
    expect(error).toContain('SUNAT 1001');
  });

  it('avisa si la API no responde', async () => {
    const fixture = TestBed.createComponent(App);
    const elemento = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();

    botones(elemento)[0].click();
    http.expectOne((r) => r.url === '/guias/ejemplo').error(new ProgressEvent('error'));
    await fixture.whenStable();

    expect(elemento.querySelector('[role=alert]')?.textContent).toContain('localhost:5142');
  });
});
