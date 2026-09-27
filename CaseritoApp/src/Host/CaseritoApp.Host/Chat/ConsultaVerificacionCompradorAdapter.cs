using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Identity.Application.Kyc;

namespace CaseritoApp.Host.Chat;

public sealed class ConsultaVerificacionCompradorAdapter(IConsultaVerificacionKyc consulta)
    : IConsultaVerificacionComprador
{
    public Task<bool> EstaHabilitadoAsync(Guid usuarioId, CancellationToken ct) =>
        consulta.EstaHabilitadoParaMarketplaceAsync(usuarioId, ct);
}
