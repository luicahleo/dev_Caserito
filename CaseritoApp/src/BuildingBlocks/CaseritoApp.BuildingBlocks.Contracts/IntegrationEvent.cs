using MediatR;

namespace CaseritoApp.BuildingBlocks.Contracts;

/// <summary>Marcador de evento de integración entre bounded contexts.</summary>
public interface IIntegrationEvent : INotification
{
    public Guid EventId { get; }
    public DateTimeOffset OcurridoEn { get; }
}
