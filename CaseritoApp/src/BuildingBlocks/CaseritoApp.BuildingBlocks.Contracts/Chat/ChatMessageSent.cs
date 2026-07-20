namespace CaseritoApp.BuildingBlocks.Contracts.Chat;

public sealed record ChatMessageSent(
    Guid EventId,
    DateTimeOffset OcurridoEn,
    Guid ConversacionId,
    Guid MensajeId,
    Guid RemitenteId,
    Guid DestinatarioId) : IIntegrationEvent;
