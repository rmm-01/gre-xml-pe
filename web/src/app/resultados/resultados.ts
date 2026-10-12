import { Component, input, output } from '@angular/core';
import { RespuestaEnvio, RespuestaGeneracion } from '../api/modelos';
import { base64ABytes, descargar } from './descarga';

/** Rechazo de SUNAT simulado antes de dar ticket (por ejemplo, 2223: la guía ya fue aceptada). */
export interface Rechazo {
  codigo: string;
  detalle: string;
}

/** Lo que devolvió la API al generar o enviar la guía. */
@Component({
  selector: 'app-resultados',
  templateUrl: './resultados.html',
  styleUrl: './resultados.scss',
})
export class Resultados {
  readonly generacion = input<RespuestaGeneracion | null>(null);
  readonly envio = input<RespuestaEnvio | null>(null);
  readonly rechazo = input<Rechazo | null>(null);
  readonly cargando = input(false);

  /** Pide a App que consulte otra vez el ticket que sigue en proceso. */
  readonly consultar = output<string>();

  protected descargarZip(generacion: RespuestaGeneracion): void {
    descargar(generacion.nombreArchivo, base64ABytes(generacion.zipBase64), 'application/zip');
  }
}
