namespace CaseritoApp.BuildingBlocks.Contracts.Identity;

public sealed record UserVerified(
    Guid EventId,
    DateTimeOffset OcurridoEn,
    Guid UserId) : IIntegrationEvent;
