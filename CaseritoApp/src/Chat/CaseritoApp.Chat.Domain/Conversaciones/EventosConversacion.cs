using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Chat.Domain.Conversaciones;

public sealed record ConversacionIniciada(
    Guid ConversacionId,
    Guid AvisoId,
    DateTimeOffset OcurridoEn) : IDomainEvent;
