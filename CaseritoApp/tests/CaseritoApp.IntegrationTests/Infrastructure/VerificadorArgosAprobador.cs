using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Kyc;

namespace CaseritoApp.IntegrationTests.Infrastructure;

/// <summary>Fake de ARGOS que aprueba automáticamente cualquier par documento/selfie.</summary>
public sealed class VerificadorArgosAprobador : IVerificadorIdentidadArgos
{
    public Task<Result<VerificacionFacialResultado>> VerificarAsync(
        byte[] imagenDocumento, byte[] imagenSelfie, CancellationToken ct) =>
        Task.FromResult(Result.Exito(new VerificacionFacialResultado(true, 99.0, null)));
}
