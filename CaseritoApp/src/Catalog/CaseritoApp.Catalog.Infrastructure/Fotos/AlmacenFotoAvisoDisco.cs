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
        Directory.CreateDirectory(_rutaBase);
        var clave = Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(RutaBlob(clave), contenido, ct);
        await File.WriteAllTextAsync(RutaMeta(clave), contentType, ct);
        return clave;
    }

    public async Task<(byte[] Contenido, string ContentType)> ObtenerAsync(string clave, CancellationToken ct)
    {
        var contenido = await File.ReadAllBytesAsync(RutaBlob(clave), ct);
        var contentType = await File.ReadAllTextAsync(RutaMeta(clave), ct);
        return (contenido, contentType);
    }

    public Task EliminarAsync(string clave, CancellationToken ct)
    {
        File.Delete(RutaBlob(clave));
        File.Delete(RutaMeta(clave));
        return Task.CompletedTask;
    }

    private string RutaBlob(string clave) => Path.Combine(_rutaBase, $"{clave}.bin");
    private string RutaMeta(string clave) => Path.Combine(_rutaBase, $"{clave}.meta");
}
