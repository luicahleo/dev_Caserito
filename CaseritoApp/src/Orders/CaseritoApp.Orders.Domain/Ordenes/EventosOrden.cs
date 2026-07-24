using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Orders.Domain.Ordenes;

public sealed record OrdenSolicitada(
    Guid OrdenId,
    Guid AvisoId,
    DateTimeOffset OcurridoEn) : IDomainEvent;

public sealed record EstadoOrdenCambiado(
    Guid OrdenId,
    EstadoOrden EstadoAnterior,
    EstadoOrden EstadoNuevo,
    DateTimeOffset OcurridoEn) : IDomainEvent;
