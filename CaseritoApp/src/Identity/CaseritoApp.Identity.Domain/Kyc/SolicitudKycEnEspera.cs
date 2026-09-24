using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Domain.Kyc;

/// <summary>
/// Evento de dominio publicado cuando una solicitud KYC queda esperando revisión humana
/// porque la evidencia facial no fue concluyente. Lo consume el handler que avisa a la
/// administración por correo.
/// </summary>
public sealed record SolicitudKycEnEspera(
    Guid EventoId,
    DateTimeOffset OcurridoEn,
    Guid UsuarioId,
    Guid SolicitudId) : IDomainEvent;
