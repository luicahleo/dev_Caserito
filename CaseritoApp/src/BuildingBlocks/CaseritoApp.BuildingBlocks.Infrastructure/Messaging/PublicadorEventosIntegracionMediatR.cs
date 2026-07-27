using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Contracts;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.BuildingBlocks.Infrastructure.Messaging;

public sealed partial class PublicadorEventosIntegracionMediatR(
    IPublisher publisher,
    ILogger<PublicadorEventosIntegracionMediatR> logger)
    : IPublicadorEventosIntegracion
{
    public async Task PublicarAsync(IIntegrationEvent evento, CancellationToken ct)
    {
        RegistrarEvento(logger, evento.GetType().Name, evento.EventId);
        await publisher.Publish(evento, ct);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Evento de integración publicado: tipo={Tipo} eventId={EventId}")]
    private static partial void RegistrarEvento(ILogger logger, string tipo, Guid eventId);
}
