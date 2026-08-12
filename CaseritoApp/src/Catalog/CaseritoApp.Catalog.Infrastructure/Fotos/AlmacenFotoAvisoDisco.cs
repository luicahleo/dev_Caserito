using CaseritoApp.Catalog.Application.Fotos;
using Microsoft.Extensions.Options;

namespace CaseritoApp.Catalog.Infrastructure.Fotos;

/// <summary>
/// Adaptador de <see cref="IAlmacenFotosAviso"/> que escribe blobs a disco.
/// Sin cifrado: las fotos de producto no son PII.
/// En prod se sustituye por object storage sin tocar dominio ni casos de uso.
/// </summary>
public sealed class AlmacenFotoAvisoDisco(IOptions<OpcionesAlmacenFotos> opciones) : IAlmacenFotosAviso
{
    private readonly string _rutaBase = opciones.Value.RutaBase;

    public async Task<string> GuardarAsync(byte[] contenido, string contentType, CancellationToken ct)
    {
        if (!string.Equals(contentType, "image/jpeg", StringComparison.Ordinal))
        {
            throw new ArgumentException("Solo se almacenan fotos normalizadas.", nameof(contentType));
        }

        var clave = Guid.NewGuid().ToString("N");
        var rutaFinal = RutaJpeg(clave);
        var directorio = Path.GetDirectoryName(rutaFinal)!;
        Directory.CreateDirectory(directorio);
        var rutaTemporal = Path.Combine(directorio, $"{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(rutaTemporal, contenido, ct);
            File.Move(rutaTemporal, rutaFinal);
            return clave;
        }
        finally
        {
            File.Delete(rutaTemporal);
        }
    }

    public async Task<(byte[] Contenido, string ContentType)> ObtenerAsync(string clave, CancellationToken ct)
    {
        ValidarClave(clave);
        var rutaJpeg = RutaJpeg(clave);
        if (File.Exists(rutaJpeg))
        {
            return (await File.ReadAllBytesAsync(rutaJpeg, ct), "image/jpeg");
        }

        var contenido = await File.ReadAllBytesAsync(RutaBlobLegado(clave), ct);
        var contentType = await File.ReadAllTextAsync(RutaMetaLegado(clave), ct);
        return (contenido, contentType);
    }

    public Task EliminarAsync(string clave, CancellationToken ct)
    {
        ValidarClave(clave);
        ct.ThrowIfCancellationRequested();
        EliminarSiExiste(RutaJpeg(clave));
        EliminarSiExiste(RutaBlobLegado(clave));
        EliminarSiExiste(RutaMetaLegado(clave));
        return Task.CompletedTask;
    }

    private string RutaJpeg(string clave) =>
        Path.Combine(_rutaBase, clave[..2], clave[2..4], $"{clave}.jpg");

    private string RutaBlobLegado(string clave) => Path.Combine(_rutaBase, $"{clave}.bin");
    private string RutaMetaLegado(string clave) => Path.Combine(_rutaBase, $"{clave}.meta");

    private static void ValidarClave(string clave)
    {
        if (!Guid.TryParseExact(clave, "N", out _))
        {
            throw new FileNotFoundException("La foto no existe.");
        }
    }

    private static void EliminarSiExiste(string ruta)
    {
        if (File.Exists(ruta))
        {
            File.Delete(ruta);
        }
    }
}
