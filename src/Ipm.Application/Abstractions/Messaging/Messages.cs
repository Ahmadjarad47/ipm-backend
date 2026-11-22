using Ipm.Domain.Common;

namespace Ipm.Application.Abstractions.Messaging;

// CQRS contracts on top of the Mediator library.
// Commands change state and return Result; queries never change state and return Result<T>.
// Every use case of the system is one command or one query with exactly one handler.

/// <summary>Marker shared by every command (used to constrain command-only pipeline behaviors).</summary>
public interface IBaseCommand;

/// <summary>A command without a return value.</summary>
public interface ICommand : Mediator.ICommand<Result>, IBaseCommand;

/// <summary>A command that returns a value (usually the id of what it created).</summary>
public interface ICommand<TResponse> : Mediator.ICommand<Result<TResponse>>, IBaseCommand;

/// <summary>A read-only request.</summary>
public interface IQuery<TResponse> : Mediator.IQuery<Result<TResponse>>;

public interface ICommandHandler<in TCommand> : Mediator.ICommandHandler<TCommand, Result>
    where TCommand : ICommand;

public interface ICommandHandler<in TCommand, TResponse> : Mediator.ICommandHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;

public interface IQueryHandler<in TQuery, TResponse> : Mediator.IQueryHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
