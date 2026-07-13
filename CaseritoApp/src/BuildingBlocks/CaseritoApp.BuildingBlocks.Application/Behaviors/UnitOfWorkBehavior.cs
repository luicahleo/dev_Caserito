using CaseritoApp.BuildingBlocks.Application.Abstractions;
using MediatR;

namespace CaseritoApp.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Behavior de MediatR que, tras ejecutar el handler, invoca <see cref="IUnitOfWork.GuardarCambiosAsync"/>
/// una única vez. En este esqueleto no despacha eventos de dominio por su cuenta: esa conexión se
/// realizará al implementar las features concretas, usando <see cref="IDomainEventDispatcher"/> de forma
/// aislada y probada por separado.
/// </summary>
public sealed class UnitOfWorkBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var respuesta = await next();
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return respuesta;
    }
}
