using CaseritoApp.Identity.Application.Kyc;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>
/// Implementación append-only (log) de <see cref="IAuditorAccesoPii"/>: registra recurso + actor de
/// cada acceso a PII. Nunca registra la PII ni la clave del blob.
/// </summary>
public sealed partial class AuditorAccesoPiiLog(ILogger<AuditorAccesoPiiLog> logger) : IAuditorAccesoPii
{
    public Task RegistrarAccesoAsync(string recurso, string actor, CancellationToken ct)
    {
        RegistrarAcceso(logger, recurso, actor);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Acceso a PII: recurso={Recurso} actor={Actor}")]
    private static partial void RegistrarAcceso(ILogger logger, string recurso, string actor);
}
