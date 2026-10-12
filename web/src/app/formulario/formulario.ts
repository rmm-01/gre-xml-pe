import { Component, inject, input } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { TIPOS_DOCUMENTO } from '../api/modelos';
import {
  FormularioGuia,
  MOTIVO_CON_PROVEEDOR,
  nuevoBien,
  nuevoConductor,
  nuevoVehiculo,
} from './guia-formulario';

/**
 * Las secciones de la guía. El motivo se elige arriba (en App) porque decide si se pide el proveedor;
 * la modalidad, aquí, decide si se pide el transportista o los vehículos y conductores.
 */
@Component({
  selector: 'app-formulario',
  imports: [ReactiveFormsModule],
  templateUrl: './formulario.html',
  styleUrl: './formulario.scss',
})
export class Formulario {
  private readonly fb = inject(NonNullableFormBuilder);

  readonly form = input.required<FormularioGuia>();

  protected readonly tiposDocumento = TIPOS_DOCUMENTO;

  protected conProveedor(): boolean {
    return this.form().controls.motivoTraslado.value === MOTIVO_CON_PROVEEDOR;
  }

  protected esPublico(): boolean {
    return this.form().controls.transporte.controls.modalidad.value === 'Publico';
  }

  protected agregarBien(): void {
    this.form().controls.bienes.push(nuevoBien(this.fb));
  }

  protected agregarVehiculo(): void {
    this.form().controls.transporte.controls.vehiculos.push(nuevoVehiculo(this.fb));
  }

  protected agregarConductor(): void {
    this.form().controls.transporte.controls.conductores.push(nuevoConductor(this.fb));
  }
}
