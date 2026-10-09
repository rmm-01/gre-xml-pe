using System.IO.Compression;

namespace GreXml.Core.Envio;

/// <summary>Crea y lee ZIP de un archivo, con un límite de tamaño al descomprimir.</summary>
internal static class ZipSeguro
{
    // Un XML de guía pesa unos pocos KB. El límite protege de un "ZIP bomba": pocos KB que se expanden a GB.
    public const int MaxBytesPorArchivo = 10 * 1024 * 1024;

    public static byte[] Crear(string nombre, byte[] contenido)
    {
        using var memoria = new MemoryStream();
        using (var archivo = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var flujo = archivo.CreateEntry(nombre, CompressionLevel.Optimal).Open();
            flujo.Write(contenido);
        }
        return memoria.ToArray();
    }

    /// <summary>Archivos del ZIP (sin carpetas). Lanza InvalidDataException si está dañado o es demasiado grande.</summary>
    public static IReadOnlyList<(string Nombre, byte[] Contenido)> Leer(byte[] zip)
    {
        using var archivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var archivos = new List<(string, byte[])>();

        foreach (var entrada in archivo.Entries)
        {
            if (entrada.FullName.EndsWith('/'))
                continue;

            // Se cuenta lo que realmente se descomprime: el tamaño declarado en el ZIP puede ser falso.
            using var flujo = entrada.Open();
            using var destino = new MemoryStream();
            var bufer = new byte[81920];
            int leidos;
            while ((leidos = flujo.Read(bufer)) > 0)
            {
                if (destino.Length + leidos > MaxBytesPorArchivo)
                    throw new InvalidDataException($"'{entrada.FullName}' supera el tamaño máximo permitido.");
                destino.Write(bufer, 0, leidos);
            }

            archivos.Add((entrada.FullName, destino.ToArray()));
        }

        return archivos;
    }
}
