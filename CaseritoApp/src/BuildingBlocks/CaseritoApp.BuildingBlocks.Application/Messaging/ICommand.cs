using CaseritoApp.BuildingBlocks.Domain;
using MediatR;

namespace CaseritoApp.BuildingBlocks.Application.Messaging;

/// <summary>
/// Marca un comando de aplicación sin valor de retorno; el handler produce un <see cref="Result"/>.
/// </summary>
public interface ICommand : IRequest<Result>;

/// <summary>
/// Marca un comando de aplicación con valor de retorno; el handler produce un <see cref="Result{TResponse}"/>.
/// </summary>
/// <typeparam name="TResponse">Tipo del valor producido cuando el comando tiene éxito.</typeparam>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>;

/// <summary>
/// Handler para un <see cref="ICommand"/> sin valor de retorno.
/// </summary>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand;

/// <summary>
/// Handler para un <see cref="ICommand{TResponse}"/> con valor de retorno.
/// </summary>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;
