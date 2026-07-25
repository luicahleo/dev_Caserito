using CaseritoApp.BuildingBlocks.Application.Messaging;
using FluentValidation;

namespace CaseritoApp.Reputation.Application.Resenas;

public sealed record ListarResenasPublicasQuery(
    Guid UsuarioId,
    int Pagina,
    int Tamano) : IQuery<ResultadoPaginadoResenasDto>;

public sealed class ListarResenasPublicasQueryHandler(IConsultaResenas consulta)
    : IQueryHandler<ListarResenasPublicasQuery, ResultadoPaginadoResenasDto>
{
    public Task<ResultadoPaginadoResenasDto> Handle(
        ListarResenasPublicasQuery request,
        CancellationToken cancellationToken) =>
        consulta.ListarPublicasAsync(
            request.UsuarioId,
            request.Pagina,
            request.Tamano,
            cancellationToken);
}

public sealed class ListarResenasPublicasQueryValidator
    : AbstractValidator<ListarResenasPublicasQuery>
{
    public ListarResenasPublicasQueryValidator()
    {
        RuleFor(query => query.UsuarioId).NotEmpty();
        RuleFor(query => query.Pagina).GreaterThanOrEqualTo(1);
        RuleFor(query => query.Tamano).InclusiveBetween(1, 50);
    }
}
