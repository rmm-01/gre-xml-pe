import { JsonPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { GuiaApi } from './api/guia-api';
import { GuiaRemision, MOTIVOS, RespuestaValidacion } from './api/modelos';
import { Formulario } from './formulario/formulario';
import { aGuia, cargarGuia, crearFormulario } from './formulario/guia-formulario';

/** Pantalla principal: el formulario de la guía y las acciones contra la API. El panel de resultados llega en 7c. */
@Component({
  selector: 'app-root',
  imports: [ReactiveFormsModule, JsonPipe, Formulario],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly api = inject(GuiaApi);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly motivos = MOTIVOS;
  protected readonly form = crearFormulario(this.fb);
  protected readonly validacion = signal<RespuestaValidacion | null>(null);
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
      next: (respuesta) => this.validacion.set(respuesta),
      error: (error: unknown) => this.fallo(error),
      complete: () => this.cargando.set(false),
    });
  }

  private iniciar(): void {
    this.cargando.set(true);
    this.errorApi.set(null);
    this.validacion.set(null);
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
