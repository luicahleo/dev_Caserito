using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Orders.Domain.Ordenes;
using FluentValidation;

namespace CaseritoApp.Orders.Application.Ordenes;

public sealed record ListarOrdenesQuery(
    Guid ActorId,
    string Rol,
    string? Estado,
    int Pagina,
    int Tamano) : IQuery<ResultadoPaginadoOrdenes>;

public sealed class ListarOrdenesQueryHandler(IConsultaOrdenes consulta)
    : IQueryHandler<ListarOrdenesQuery, ResultadoPaginadoOrdenes>
{
    public Task<ResultadoPaginadoOrdenes> Handle(
        ListarOrdenesQuery request,
        CancellationToken cancellationToken)
    {
        EstadoOrden? estado = string.IsNullOrWhiteSpace(request.Estado)
            ? null
            : Enum.Parse<EstadoOrden>(request.Estado);

        return consulta.ListarAsync(
            request.ActorId,
            request.Rol,
            estado,
            request.Pagina,
            request.Tamano,
            cancellationToken);
    }
}

public sealed class ListarOrdenesQueryValidator : AbstractValidator<ListarOrdenesQuery>
{
    private static readonly string[] _rolesPermitidos = ["comprador", "vendedor"];
    private static readonly string[] _estadosPermitidos =
        [nameof(EstadoOrden.Requested), nameof(EstadoOrden.Agreed), nameof(EstadoOrden.Cancelled)];

    public ListarOrdenesQueryValidator()
    {
        RuleFor(query => query.ActorId).NotEmpty();
        RuleFor(query => query.Rol).Must(rol => _rolesPermitidos.Contains(rol, StringComparer.Ordinal));
        RuleFor(query => query.Estado)
            .Must(estado => string.IsNullOrWhiteSpace(estado)
                || _estadosPermitidos.Contains(estado, StringComparer.Ordinal));
        RuleFor(query => query.Pagina).GreaterThanOrEqualTo(1);
        RuleFor(query => query.Tamano).InclusiveBetween(1, 100);
    }
}
