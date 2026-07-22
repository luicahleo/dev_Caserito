using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Chat.Domain.Conversaciones;

public sealed record ConversacionIniciada(
    Guid ConversacionId,
    Guid AvisoId,
    DateTimeOffset OcurridoEn) : IDomainEvent;

public sealed record MensajeEnviado(
    Guid ConversacionId,
    Guid MensajeId,
    long Secuencia,
    DateTimeOffset OcurridoEn) : IDomainEvent;

public sealed record LecturaAvanzada(
    Guid ConversacionId,
    long HastaSecuencia,
    DateTimeOffset OcurridoEn) : IDomainEvent;

public sealed record ConversacionEstadoCambiado(
    Guid ConversacionId,
    EstadoConversacion Estado,
    DateTimeOffset OcurridoEn) : IDomainEvent;
