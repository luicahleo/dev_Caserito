using MediatR;

namespace CaseritoApp.BuildingBlocks.Application.Messaging;

/// <summary>
/// Marca una consulta de aplicación que produce un valor de tipo <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TResponse">Tipo del valor producido por la consulta.</typeparam>
public interface IQuery<out TResponse> : IRequest<TResponse>;

/// <summary>
/// Handler para un <see cref="IQuery{TResponse}"/>.
/// </summary>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>;
