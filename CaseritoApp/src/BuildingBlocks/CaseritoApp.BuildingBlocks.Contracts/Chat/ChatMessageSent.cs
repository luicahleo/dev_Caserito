namespace CaseritoApp.BuildingBlocks.Contracts.Chat;

public sealed record ChatMessageSent(
    Guid EventId,
    DateTimeOffset OcurridoEn,
    Guid ConversacionId,
    Guid MensajeId,
    long Secuencia,
    Guid RemitenteId,
    Guid DestinatarioId) : IIntegrationEvent;
