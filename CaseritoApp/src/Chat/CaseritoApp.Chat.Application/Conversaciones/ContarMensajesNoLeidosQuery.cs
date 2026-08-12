using CaseritoApp.BuildingBlocks.Application.Messaging;
using FluentValidation;

namespace CaseritoApp.Chat.Application.Conversaciones;

public sealed record ContarMensajesNoLeidosQuery(Guid UsuarioId) : IQuery<int>;

public sealed class ContarMensajesNoLeidosQueryHandler(IConsultaConversaciones consulta)
    : IQueryHandler<ContarMensajesNoLeidosQuery, int>
{
    public Task<int> Handle(
        ContarMensajesNoLeidosQuery request,
        CancellationToken cancellationToken) =>
        consulta.ContarNoLeidosAsync(request.UsuarioId, cancellationToken);
}

public sealed class ContarMensajesNoLeidosQueryValidator
    : AbstractValidator<ContarMensajesNoLeidosQuery>
{
    public ContarMensajesNoLeidosQueryValidator()
    {
        RuleFor(q => q.UsuarioId).NotEmpty().WithMessage("El usuario es obligatorio.");
    }
}
