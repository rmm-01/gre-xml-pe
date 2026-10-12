import { Component, inject, input } from '@angular/core';
import { ControlContainer } from '@angular/forms';
import { erroresApi } from './errores';

/**
 * Muestra los errores de la API de un campo. Busca el control dentro del grupo en el que está
 * (formGroupName, formArrayName): sin `nombre`, muestra los errores del propio grupo o lista.
 */
@Component({
  selector: 'app-error-campo',
  template: `
    @for (e of errores(); track e.codigo) {
      <small class="error-campo" role="alert">
        {{ e.mensaje }}
        <span>{{ e.codigo }}{{ e.codigoSunat ? ' · SUNAT ' + e.codigoSunat : '' }}</span>
      </small>
    }
  `,
  styles: `
    :host {
      display: contents;
    }
    .error-campo {
      display: block;
      max-width: 22rem;
      color: var(--color-error);
      font-size: 0.8rem;
    }
    span {
      margin-left: 0.4rem;
      color: var(--color-suave);
    }
  `,
})
export class ErrorCampo {
  private readonly contenedor = inject(ControlContainer);

  readonly nombre = input<string | number>();

  protected errores() {
    const grupo = this.contenedor.control;
    const nombre = this.nombre();
    return erroresApi(nombre === undefined ? grupo : (grupo?.get(String(nombre)) ?? null));
  }
}
