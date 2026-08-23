using LeaveLite.Domain.Common;

namespace LeaveLite.Application.Abstractions;

/// <summary>Publishes domain events pulled from aggregates after a successful unit of work.</summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);

    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
