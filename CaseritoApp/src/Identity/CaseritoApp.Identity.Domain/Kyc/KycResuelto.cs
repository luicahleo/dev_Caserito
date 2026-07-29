using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Domain.Kyc;

/// <summary>
/// Evento de dominio publicado cuando una solicitud KYC es resuelta (aprobada o rechazada)
/// por la validación humana. Lo consume el handler que notifica al usuario por correo.
/// </summary>
public sealed record KycResuelto(
    Guid EventoId,
    DateTimeOffset OcurridoEn,
    Guid UsuarioId,
    Guid SolicitudId,
    EstadoKyc Estado,
    string? MotivoRechazo) : IDomainEvent;
