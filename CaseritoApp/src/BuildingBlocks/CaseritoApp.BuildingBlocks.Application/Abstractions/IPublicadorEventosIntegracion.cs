using CaseritoApp.BuildingBlocks.Contracts;

namespace CaseritoApp.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Publica eventos de integración entre bounded contexts. En el MVP no hay bus ni outbox: la
/// implementación registra el evento (sin PII). El outbox transaccional queda diferido.
/// </summary>
public interface IPublicadorEventosIntegracion
{
    public Task PublicarAsync(IIntegrationEvent evento, CancellationToken ct);
}
