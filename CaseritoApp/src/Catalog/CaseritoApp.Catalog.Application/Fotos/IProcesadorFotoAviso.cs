namespace CaseritoApp.Catalog.Application.Fotos;

/// <summary>Resultado seguro y normalizado para persistir.</summary>
public sealed record FotoAvisoProcesada(byte[] Contenido, string ContentType);

/// <summary>Normaliza defensivamente fotos antes de almacenarlas.</summary>
public interface IProcesadorFotoAviso
{
    /// <summary>Devuelve <see langword="null"/> cuando el contenido no puede normalizarse.</summary>
    public Task<FotoAvisoProcesada?> ProcesarAsync(
        byte[] contenido,
        string contentType,
        CancellationToken ct);
}
