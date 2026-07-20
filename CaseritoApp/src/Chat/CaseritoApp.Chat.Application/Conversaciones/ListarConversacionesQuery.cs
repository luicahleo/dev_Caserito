using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Chat.Application.Paginacion;
using FluentValidation;

namespace CaseritoApp.Chat.Application.Conversaciones;

public sealed record ListarConversacionesQuery(
    Guid UsuarioId,
    FronteraConversaciones? Frontera,
    int Limite = 20) : IQuery<PaginaCursor<ConversacionResumenDto, FronteraConversaciones>>;

public sealed class ListarConversacionesQueryHandler(IConsultaConversaciones consulta)
    : IQueryHandler<ListarConversacionesQuery, PaginaCursor<ConversacionResumenDto, FronteraConversaciones>>
{
    public Task<PaginaCursor<ConversacionResumenDto, FronteraConversaciones>> Handle(
        ListarConversacionesQuery request,
        CancellationToken cancellationToken) =>
        consulta.ListarAsync(request.UsuarioId, request.Frontera, request.Limite, cancellationToken);
}

public sealed class ListarConversacionesQueryValidator : AbstractValidator<ListarConversacionesQuery>
{
    public ListarConversacionesQueryValidator()
    {
        RuleFor(q => q.UsuarioId)
            .NotEmpty().WithMessage("El usuario es obligatorio.");
        RuleFor(q => q.Limite)
            .InclusiveBetween(1, 50).WithMessage("El límite debe estar entre 1 y 50.");
        RuleFor(q => q.Frontera)
            .Must(f => f is null || f.Value.ConversacionId != Guid.Empty)
            .WithMessage("El cursor no es válido.");
    }
}
