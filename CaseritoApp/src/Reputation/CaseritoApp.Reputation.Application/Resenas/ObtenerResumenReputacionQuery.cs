using CaseritoApp.BuildingBlocks.Application.Messaging;

namespace CaseritoApp.Reputation.Application.Resenas;

public sealed record ObtenerResumenReputacionQuery(Guid UsuarioId)
    : IQuery<ResumenReputacionDto>;

public sealed class ObtenerResumenReputacionQueryHandler(IConsultaResenas consulta)
    : IQueryHandler<ObtenerResumenReputacionQuery, ResumenReputacionDto>
{
    public Task<ResumenReputacionDto> Handle(
        ObtenerResumenReputacionQuery request,
        CancellationToken cancellationToken) =>
        consulta.ObtenerResumenAsync(request.UsuarioId, cancellationToken);
}
