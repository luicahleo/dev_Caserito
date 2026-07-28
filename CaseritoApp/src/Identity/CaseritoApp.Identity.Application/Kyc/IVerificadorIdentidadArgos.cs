using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Puerto hacia un servicio de comparación facial 1:1 (ARGOS).</summary>
public interface IVerificadorIdentidadArgos
{
    /// <summary>Compara documento y selfie. No loguea bytes ni base64.</summary>
    public Task<Result<VerificacionFacialResultado>> VerificarAsync(
        byte[] imagenDocumento, byte[] imagenSelfie, CancellationToken ct);
}

/// <summary>Resultado de la comparación facial.</summary>
/// <param name="Coinciden">true si el rostro coincide con el documento.</param>
/// <param name="SimilitudPercent">Score de similitud devuelto por ARGOS (0-100).</param>
/// <param name="MotivoRechazo">Motivo legible cuando <see cref="Coinciden"/> es false.</param>
public sealed record VerificacionFacialResultado(
    bool Coinciden,
    double SimilitudPercent,
    string? MotivoRechazo);
