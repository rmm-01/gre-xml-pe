import { base64ABytes, descargar } from './descarga';

describe('descarga', () => {
  it('convierte el Base64 en los bytes originales, incluidos los que no son texto', () => {
    // "PK\x03\x04": la firma con la que empieza todo ZIP.
    expect(Array.from(base64ABytes('UEsDBA=='))).toEqual([0x50, 0x4b, 0x03, 0x04]);
    expect(Array.from(base64ABytes(btoa('\x00\xff')))).toEqual([0, 255]);
  });

  it('descarga con el nombre indicado y libera la URL', () => {
    const crear = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:prueba');
    const liberar = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      expect(this.download).toBe('20100000050-09-T001-1.zip');
      expect(this.href).toBe('blob:prueba');
    });

    descargar('20100000050-09-T001-1.zip', new Uint8Array([1, 2]), 'application/zip');

    expect(click).toHaveBeenCalledOnce();
    expect((crear.mock.calls[0][0] as Blob).type).toBe('application/zip');
    expect(liberar).toHaveBeenCalledWith('blob:prueba');
    vi.restoreAllMocks();
  });
});
