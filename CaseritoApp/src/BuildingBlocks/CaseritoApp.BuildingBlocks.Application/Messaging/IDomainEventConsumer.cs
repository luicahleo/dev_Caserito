using CaseritoApp.BuildingBlocks.Domain;
using MediatR;

namespace CaseritoApp.BuildingBlocks.Application.Messaging;

/// <summary>
/// Consumidor de un evento de dominio. Sinónimo semántico de <see cref="INotificationHandler{TNotification}"/>.
/// </summary>
/// <typeparam name="TEvent">Tipo del evento de dominio.</typeparam>
public interface IDomainEventConsumer<in TEvent> : INotificationHandler<TEvent>
    where TEvent : IDomainEvent;
