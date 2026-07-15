using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Contracts;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>
/// Implementación in-process de <see cref="IPublicadorEventosIntegracion"/>: registra el evento sin
/// PII (tipo + EventId). No hay bus ni outbox en el MVP; el outbox transaccional queda diferido.
/// </summary>
public sealed partial class PublicadorEventosIntegracionLog(ILogger<PublicadorEventosIntegracionLog> logger)
    : IPublicadorEventosIntegracion
{
    public Task PublicarAsync(IIntegrationEvent evento, CancellationToken ct)
    {
        RegistrarEvento(logger, evento.GetType().Name, evento.EventId);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Evento de integración publicado: tipo={Tipo} eventId={EventId}")]
    private static partial void RegistrarEvento(ILogger logger, string tipo, Guid eventId);
}
