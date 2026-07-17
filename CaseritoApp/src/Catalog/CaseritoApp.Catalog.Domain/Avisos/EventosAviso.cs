using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>Se publicó un aviso nuevo (gancho del contrato de integración ProductPublished).</summary>
public sealed record AvisoPublicado(Guid AvisoId, Guid VendedorId) : IDomainEvent;

/// <summary>Se editaron los datos de un aviso.</summary>
public sealed record AvisoEditado(Guid AvisoId) : IDomainEvent;

/// <summary>Se pausó un aviso.</summary>
public sealed record AvisoPausado(Guid AvisoId) : IDomainEvent;

/// <summary>Se reactivó un aviso.</summary>
public sealed record AvisoReactivado(Guid AvisoId) : IDomainEvent;

/// <summary>Se eliminó (soft-delete) un aviso.</summary>
public sealed record AvisoEliminado(Guid AvisoId) : IDomainEvent;
