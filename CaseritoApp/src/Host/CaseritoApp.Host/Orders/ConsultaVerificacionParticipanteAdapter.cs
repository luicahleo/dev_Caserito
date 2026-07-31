using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Orders.Application.Ordenes;

namespace CaseritoApp.Host.Orders;

public sealed class ConsultaVerificacionParticipanteAdapter(IConsultaVerificacionKyc consulta)
    : IConsultaVerificacionParticipante
{
    public Task<bool> EstaVerificadoAsync(Guid usuarioId, CancellationToken ct) =>
        consulta.EstaHabilitadoParaMarketplaceAsync(usuarioId, ct);
}
