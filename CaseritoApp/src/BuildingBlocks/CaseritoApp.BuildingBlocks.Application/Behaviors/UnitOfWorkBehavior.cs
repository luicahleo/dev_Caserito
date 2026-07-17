using CaseritoApp.BuildingBlocks.Application.Abstractions;
using MediatR;

namespace CaseritoApp.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Behavior de MediatR que, tras ejecutar el handler, invoca
/// <see cref="IUnitOfWork.GuardarCambiosAsync"/> una vez en cada unidad de trabajo registrada.
/// En un monolito modular hay una por bounded context (Identity, Catalog, ...); guardar un
/// contexto sin cambios es un no-op, de modo que el handler solo persiste el contexto que tocó.
/// No despacha eventos de dominio por su cuenta: esa conexión se hará al implementar el dispatcher.
/// </summary>
public sealed class UnitOfWorkBehavior<TRequest, TResponse>(IEnumerable<IUnitOfWork> unidadesDeTrabajo)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var respuesta = await next();
        foreach (var unidad in unidadesDeTrabajo)
        {
            await unidad.GuardarCambiosAsync(cancellationToken);
        }

        return respuesta;
    }
}
