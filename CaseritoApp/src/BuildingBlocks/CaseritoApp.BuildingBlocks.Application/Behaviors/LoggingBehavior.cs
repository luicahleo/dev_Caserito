using MediatR;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Behavior de MediatR que registra el inicio y fin del procesamiento de cada request.
/// </summary>
public sealed partial class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var nombre = typeof(TRequest).Name;
        RegistrarProcesando(logger, nombre);
        var respuesta = await next();
        RegistrarProcesado(logger, nombre);
        return respuesta;
    }

    // CA1848/CA1873: se usan delegados generados por LoggerMessage (en lugar de LogInformation directo)
    // para evitar asignaciones y evaluación de argumentos innecesarias cuando el nivel está deshabilitado.
    [LoggerMessage(Level = LogLevel.Information, Message = "Procesando {Request}")]
    private static partial void RegistrarProcesando(ILogger logger, string request);

    [LoggerMessage(Level = LogLevel.Information, Message = "Procesado {Request}")]
    private static partial void RegistrarProcesado(ILogger logger, string request);
}
