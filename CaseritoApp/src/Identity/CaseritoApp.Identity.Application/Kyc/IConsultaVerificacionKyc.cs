namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>
/// Consulta de solo lectura del estado de verificación, usada fuera del pipeline de MediatR
/// (login/refresh y perfil) para derivar el badge/claim "verificado".
/// </summary>
public interface IConsultaVerificacionKyc
{
    public Task<bool> EstaVerificadoAsync(Guid usuarioId, CancellationToken ct);
}
