namespace CaseritoApp.BuildingBlocks.Contracts.Catalog;

public sealed record ProductPublished(
    Guid EventId,
    DateTimeOffset OcurridoEn,
    Guid ProductId,
    Guid SellerId) : IIntegrationEvent;
