namespace CaseritoApp.BuildingBlocks.Contracts.Orders;

public sealed record OrderStatusChanged(
    Guid EventId,
    DateTimeOffset OcurridoEn,
    Guid OrderId,
    string OldStatus,
    string NewStatus) : IIntegrationEvent;
