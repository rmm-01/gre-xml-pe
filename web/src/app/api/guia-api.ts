import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, throwError } from 'rxjs';
import {
  CodigoMotivo,
  GuiaRemision,
  RespuestaEnvio,
  RespuestaGeneracion,
  RespuestaValidacion,
  ResultadoApi,
} from './modelos';

/** Acceso a la API. En desarrollo, el proxy de `ng serve` reenvía /guias y /envios a localhost:5142. */
@Injectable({ providedIn: 'root' })
export class GuiaApi {
  private readonly http = inject(HttpClient);

  ejemplo(motivo: CodigoMotivo): Observable<GuiaRemision> {
    return this.http.get<GuiaRemision>('/guias/ejemplo', { params: { motivo } });
  }

  validar(guia: GuiaRemision): Observable<RespuestaValidacion> {
    return this.http.post<RespuestaValidacion>('/guias/validar', guia);
  }

  generar(guia: GuiaRemision): Observable<ResultadoApi<RespuestaGeneracion>> {
    return this.conResultado(this.http.post<RespuestaGeneracion>('/guias/generar', guia));
  }

  enviar(guia: GuiaRemision): Observable<ResultadoApi<RespuestaEnvio>> {
    return this.conResultado(this.http.post<RespuestaEnvio>('/guias/enviar', guia));
  }

  consultarEnvio(ticket: string): Observable<RespuestaEnvio> {
    return this.http.get<RespuestaEnvio>(`/envios/${encodeURIComponent(ticket)}`);
  }

  /**
   * Un 422 es una respuesta esperada (la guía tiene errores o SUNAT simulado no aceptó el archivo), no un fallo:
   * se convierte en un resultado. Cualquier otro error (red, 400, 500) sigue siendo un error.
   */
  private conResultado<T>(peticion: Observable<T>): Observable<ResultadoApi<T>> {
    return peticion.pipe(
      map((datos): ResultadoApi<T> => ({ tipo: 'ok', datos })),
      catchError((error: unknown) => {
        if (!(error instanceof HttpErrorResponse) || error.status !== 422) return throwError(() => error);

        const cuerpo = error.error as Partial<RespuestaValidacion> & { codigo?: string; detail?: string };
        if (Array.isArray(cuerpo?.errores)) return of<ResultadoApi<T>>({ tipo: 'invalida', errores: cuerpo.errores });

        return of<ResultadoApi<T>>({ tipo: 'rechazada', codigo: cuerpo?.codigo ?? '', detalle: cuerpo?.detail ?? '' });
      }),
    );
  }
}
