namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>
/// Consulta de solo lectura del estado de verificación, usada fuera del pipeline de MediatR
/// (login/refresh y perfil) para derivar el badge/claim "verificado".
/// </summary>
public interface IConsultaVerificacionKyc
{
    public Task<bool> EstaVerificadoAsync(Guid usuarioId, CancellationToken ct);

    /// <summary>Indica si el usuario tiene KYC aprobado o posee el rol AdminPlataforma.</summary>
    public Task<bool> EstaHabilitadoParaMarketplaceAsync(Guid usuarioId, CancellationToken ct);

    /// <summary>Datos mínimos del usuario para notificarle el resultado de su KYC.</summary>
    public Task<UsuarioKycDto?> ObtenerUsuarioAsync(Guid usuarioId, CancellationToken ct);
}

/// <summary>Contacto del usuario al que se le notifica la resolución de su KYC.</summary>
public sealed record UsuarioKycDto(string Email, string Nombre);
