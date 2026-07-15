using CaseritoApp.BuildingBlocks.Infrastructure.Security;
using CaseritoApp.Identity.Application.Kyc;
using Microsoft.Extensions.Options;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>
/// Adaptador de desarrollo de <see cref="IAlmacenBlobsKyc"/>: escribe blobs cifrados a disco, fuera
/// de la BD, con claves opacas. El content-type va en un sidecar (no es PII). En prod se sustituye
/// por object storage + KMS sin tocar dominio ni casos de uso.
/// </summary>
public sealed class AlmacenBlobsKycDisco(IEncryptor encryptor, IOptions<OpcionesAlmacenKyc> opciones)
    : IAlmacenBlobsKyc
{
    private readonly string _rutaBase = opciones.Value.RutaBase;

    public async Task<string> GuardarAsync(byte[] contenido, string contentType, CancellationToken ct)
    {
        Directory.CreateDirectory(_rutaBase);
        var clave = Guid.NewGuid().ToString("N");
        var cifrado = encryptor.Cifrar(contenido);
        await File.WriteAllBytesAsync(RutaBlob(clave), cifrado, ct);
        await File.WriteAllTextAsync(RutaMeta(clave), contentType, ct);
        return clave;
    }

    public async Task<BlobKyc> ObtenerAsync(string clave, CancellationToken ct)
    {
        var cifrado = await File.ReadAllBytesAsync(RutaBlob(clave), ct);
        var contenido = encryptor.Descifrar(cifrado);
        var contentType = await File.ReadAllTextAsync(RutaMeta(clave), ct);
        return new BlobKyc(contenido, contentType);
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
