using MediatR;

namespace CaseritoApp.BuildingBlocks.Domain;

/// <summary>
/// Marca un evento de dominio, permitiendo su publicación a través de MediatR.
/// </summary>
public interface IDomainEvent : INotification;
