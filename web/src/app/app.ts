import { JsonPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { GuiaApi } from './api/guia-api';
import { CodigoMotivo, GuiaRemision, MOTIVOS, RespuestaValidacion } from './api/modelos';

/** Pantalla mínima (hito 7a): carga la guía de ejemplo y la valida contra la API. El formulario llega en 7b. */
@Component({
  selector: 'app-root',
  imports: [FormsModule, JsonPipe],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly api = inject(GuiaApi);

  protected readonly motivos = MOTIVOS;
  protected readonly motivo = signal<CodigoMotivo>('01');
  protected readonly guia = signal<GuiaRemision | null>(null);
  protected readonly validacion = signal<RespuestaValidacion | null>(null);
  protected readonly cargando = signal(false);
  protected readonly errorConexion = signal<string | null>(null);

  protected cargarEjemplo(): void {
    this.iniciar();
    this.api.ejemplo(this.motivo()).subscribe({
      next: (guia) => this.guia.set(guia),
      error: () => this.fallo(),
      complete: () => this.cargando.set(false),
    });
  }

  protected validar(): void {
    const guia = this.guia();
    if (!guia) return;

    this.iniciar();
    this.api.validar(guia).subscribe({
      next: (respuesta) => this.validacion.set(respuesta),
      error: () => this.fallo(),
      complete: () => this.cargando.set(false),
    });
  }

  private iniciar(): void {
    this.cargando.set(true);
    this.errorConexion.set(null);
    this.validacion.set(null);
  }

  private fallo(): void {
    this.cargando.set(false);
    this.errorConexion.set('No se pudo conectar con la API. ¿Está ejecutándose en http://localhost:5142?');
  }
}
