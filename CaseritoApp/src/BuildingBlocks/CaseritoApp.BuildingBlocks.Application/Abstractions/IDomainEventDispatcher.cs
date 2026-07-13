using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Abstracción para publicar eventos de dominio acumulados por los agregados.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Publica los eventos de dominio indicados.
    /// </summary>
    public Task PublicarAsync(IEnumerable<IDomainEvent> eventos, CancellationToken ct);
}
