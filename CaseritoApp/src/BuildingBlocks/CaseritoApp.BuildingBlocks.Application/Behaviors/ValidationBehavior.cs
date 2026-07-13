using FluentValidation;
using MediatR;

namespace CaseritoApp.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Behavior de MediatR que ejecuta los validadores de FluentValidation registrados para
/// <typeparamref name="TRequest"/> antes de invocar al handler. Lanza <see cref="ValidationException"/>
/// si hay fallos; la traducción a un resultado amigable para el cliente (Result/ProblemDetails) se
/// realiza en el host (fuera de este esqueleto).
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validadores)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var contexto = new ValidationContext<TRequest>(request);
        var fallos = validadores
            .Select(v => v.Validate(contexto))
            .SelectMany(r => r.Errors)
            .Where(e => e is not null)
            .ToList();

        if (fallos.Count != 0)
        {
            throw new ValidationException(fallos);
        }

        return await next();
    }
}
