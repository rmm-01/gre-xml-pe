import { AbstractControl, FormArray, FormGroup } from '@angular/forms';
import { ErrorValidacion } from '../api/modelos';

/** Clave con la que los errores de la API se guardan en `control.errors`. */
export const ERROR_API = 'api';

/** La API usa rutas del JSON ("bienes[1].cantidad"); el formulario, rutas con puntos ("bienes.1.cantidad"). */
export function rutaFormulario(campo: string): string {
  return campo.replace(/\[(\d+)\]/g, '.$1');
}

/**
 * Borra los errores de la validación anterior y guarda cada error nuevo en el control de su campo.
 * Angular los quita solo cuando el usuario cambia el campo: recalcula la validez con los validadores
 * del control, y aquí no hay ninguno. Un campo que no está en el formulario se ignora: el error
 * igual aparece en la lista general.
 */
export function marcarErrores(form: FormGroup, errores: readonly ErrorValidacion[]): void {
  limpiarErrores(form);

  const porControl = new Map<AbstractControl, ErrorValidacion[]>();
  for (const error of errores) {
    const control = form.get(rutaFormulario(error.campo));
    if (control) porControl.set(control, [...(porControl.get(control) ?? []), error]);
  }

  for (const [control, lista] of porControl) {
    control.setErrors({ [ERROR_API]: lista });
    control.markAsTouched();
  }
}

export function limpiarErrores(control: AbstractControl): void {
  if (control instanceof FormGroup || control instanceof FormArray) {
    Object.values(control.controls).forEach(limpiarErrores);
  }
  control.setErrors(null);
}

export function erroresApi(control: AbstractControl | null): ErrorValidacion[] {
  return (control?.errors?.[ERROR_API] as ErrorValidacion[] | undefined) ?? [];
}
