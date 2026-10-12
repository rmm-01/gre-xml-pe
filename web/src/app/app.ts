import { JsonPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { GuiaApi } from './api/guia-api';
import {
  ErrorValidacion,
  GuiaRemision,
  MOTIVOS,
  RespuestaEnvio,
  RespuestaGeneracion,
  ResultadoApi,
} from './api/modelos';
import { marcarErrores } from './formulario/errores';
import { Formulario } from './formulario/formulario';
import { aGuia, cargarGuia, crearFormulario } from './formulario/guia-formulario';
import { Rechazo, Resultados } from './resultados/resultados';

/** Pantalla principal: el formulario de la guía, las acciones contra la API y sus resultados. */
@Component({
  selector: 'app-root',
  imports: [ReactiveFormsModule, JsonPipe, Formulario, Resultados],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly api = inject(GuiaApi);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly motivos = MOTIVOS;
  protected readonly form = crearFormulario(this.fb);
  /** null = todavía no se validó; [] = la guía cumple todas las reglas. */
  protected readonly errores = signal<ErrorValidacion[] | null>(null);
  protected readonly generacion = signal<RespuestaGeneracion | null>(null);
  protected readonly envio = signal<RespuestaEnvio | null>(null);
  protected readonly rechazo = signal<Rechazo | null>(null);
  protected readonly cargando = signal(false);
  protected readonly errorApi = signal<string | null>(null);

  /** Lo que se enviará a la API; se muestra para ver cómo el motivo y la modalidad cambian el JSON. */
  protected guia(): GuiaRemision {
    return aGuia(this.form);
  }

  protected cargarEjemplo(): void {
    this.iniciar();
    this.api.ejemplo(this.form.controls.motivoTraslado.value).subscribe({
      next: (guia) => cargarGuia(this.fb, this.form, guia),
      error: (error: unknown) => this.fallo(error),
      complete: () => this.cargando.set(false),
    });
  }

  protected validar(): void {
    this.iniciar();
    this.api.validar(this.guia()).subscribe({
      next: (respuesta) => this.mostrarErrores(respuesta.errores),
      error: (error: unknown) => this.fallo(error),
      complete: () => this.cargando.set(false),
    });
  }

  protected generar(): void {
    this.ejecutar(this.api.generar(this.guia()), (datos) => this.generacion.set(datos));
  }

  protected enviar(): void {
    this.ejecutar(this.api.enviar(this.guia()), (datos) => this.envio.set(datos));
  }

  protected consultarEnvio(ticket: string): void {
    this.cargando.set(true);
    this.errorApi.set(null);
    this.api.consultarEnvio(ticket).subscribe({
      next: (respuesta) => this.envio.set(respuesta),
      error: (error: unknown) => this.fallo(error),
      complete: () => this.cargando.set(false),
    });
  }

  /** Generar y enviar validan primero: un 422 trae los errores (invalida) o el rechazo de SUNAT simulado. */
  private ejecutar<T>(peticion: Observable<ResultadoApi<T>>, ok: (datos: T) => void): void {
    this.iniciar();
    peticion.subscribe({
      next: (resultado) => {
        switch (resultado.tipo) {
          case 'ok':
            this.mostrarErrores([]);
            ok(resultado.datos);
            break;
          case 'invalida':
            this.mostrarErrores(resultado.errores);
            break;
          case 'rechazada':
            this.rechazo.set({ codigo: resultado.codigo, detalle: resultado.detalle });
            break;
        }
      },
      error: (error: unknown) => this.fallo(error),
      complete: () => this.cargando.set(false),
    });
  }

  private mostrarErrores(errores: ErrorValidacion[]): void {
    this.errores.set(errores);
    marcarErrores(this.form, errores);
  }

  private iniciar(): void {
    this.cargando.set(true);
    this.errorApi.set(null);
    this.errores.set(null);
    this.generacion.set(null);
    this.envio.set(null);
    this.rechazo.set(null);
  }

  private fallo(error: unknown): void {
    this.cargando.set(false);
    this.errorApi.set(
      error instanceof HttpErrorResponse && error.status === 400
        ? 'La API no pudo leer la guía: revisa que las fechas y la hora tengan un valor.'
        : 'No se pudo conectar con la API. ¿Está ejecutándose en http://localhost:5142?',
    );
  }
}
