/** Convierte el Base64 que devuelve la API en los bytes originales del archivo. */
export function base64ABytes(base64: string): Uint8Array<ArrayBuffer> {
  const texto = atob(base64);
  const bytes = new Uint8Array(texto.length);
  for (let i = 0; i < texto.length; i++) bytes[i] = texto.charCodeAt(i);
  return bytes;
}

/** Descarga los bytes como archivo: el navegador lo guarda con `nombre`, sin pasar por el servidor. */
export function descargar(nombre: string, bytes: Uint8Array<ArrayBuffer>, tipo: string): void {
  const url = URL.createObjectURL(new Blob([bytes], { type: tipo }));
  const enlace = document.createElement('a');
  enlace.href = url;
  enlace.download = nombre;
  enlace.click();
  URL.revokeObjectURL(url);
}
