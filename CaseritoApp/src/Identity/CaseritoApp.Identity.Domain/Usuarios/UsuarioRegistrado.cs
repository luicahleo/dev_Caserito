using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Domain.Usuarios;

public sealed record UsuarioRegistrado(
    Guid EventoId,
    DateTimeOffset OcurridoEn,
    Guid UsuarioId,
    string Email,
    string Nombre) : IDomainEvent;
