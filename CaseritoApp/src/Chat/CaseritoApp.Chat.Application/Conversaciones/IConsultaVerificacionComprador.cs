namespace CaseritoApp.Chat.Application.Conversaciones;

/// <summary>
/// Estado de verificación de identidad del comprador. Es un puerto porque Chat no puede
/// referenciar Identity: el Host provee el adaptador.
/// </summary>
public interface IConsultaVerificacionComprador
{
    /// <summary>KYC aprobado, o exención por rol de plataforma.</summary>
    public Task<bool> EstaHabilitadoAsync(Guid usuarioId, CancellationToken ct);
}
