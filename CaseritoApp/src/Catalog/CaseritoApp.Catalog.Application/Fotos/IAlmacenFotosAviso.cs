namespace CaseritoApp.Catalog.Application.Fotos;

/// <summary>
/// Puerto de almacenamiento de fotos de avisos. Sin cifrado: las fotos de producto no son PII.
/// El adaptador de dev escribe a disco; en prod se sustituye por object storage
/// sin tocar dominio ni casos de uso.
/// </summary>
public interface IAlmacenFotosAviso
{
    /// <summary>Guarda el contenido y devuelve una clave opaca para recuperarlo.</summary>
    public Task<string> GuardarAsync(byte[] contenido, string contentType, CancellationToken ct);

    /// <summary>Recupera el contenido y el content-type asociados a la clave.</summary>
    public Task<(byte[] Contenido, string ContentType)> ObtenerAsync(string clave, CancellationToken ct);

    /// <summary>Elimina el blob asociado a la clave (idempotente si no existe).</summary>
    public Task EliminarAsync(string clave, CancellationToken ct);
}
