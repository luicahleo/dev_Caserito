namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>
/// Almacén de blobs de PII (documento/selfie). Cifra en reposo (envelope) dentro del adaptador; el
/// resto del sistema solo maneja claves opacas. El adaptador de dev escribe a disco.
/// </summary>
public interface IAlmacenBlobsKyc
{
    /// <summary>Guarda el contenido cifrado y devuelve una clave opaca para recuperarlo.</summary>
    public Task<string> GuardarAsync(byte[] contenido, string contentType, CancellationToken ct);

    /// <summary>Recupera y descifra el blob asociado a la clave.</summary>
    public Task<BlobKyc> ObtenerAsync(string clave, CancellationToken ct);

    /// <summary>Elimina el blob asociado a la clave (idempotente si no existe).</summary>
    public Task EliminarAsync(string clave, CancellationToken ct);
}
