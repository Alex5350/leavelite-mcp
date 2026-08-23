using ErrorOr;

namespace LeaveLite.Application.Common;

/// <summary>Marker for a command (writes state, returns <see cref="Success"/>).</summary>
public interface ICommand;

/// <summary>Marker for a command that produces a value.</summary>
public interface ICommand<TResponse> : ICommand;

/// <summary>Marker for a query (reads state, no side effects).</summary>
public interface IQuery<TResponse>;

public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<ErrorOr<Success>> Handle(TCommand command, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<ErrorOr<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<ErrorOr<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}
