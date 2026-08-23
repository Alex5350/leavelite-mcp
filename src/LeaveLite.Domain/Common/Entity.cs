namespace LeaveLite.Domain.Common;

/// <summary>
/// Base class for entities with a strongly-typed identity.
/// Provides identity equality and a domain-event collection
/// that is drained via <see cref="PullDomainEvents"/> after persistence.
/// </summary>
/// <typeparam name="TEntityId">The strongly-typed identifier of the entity.</typeparam>
public abstract class Entity<TEntityId>
    where TEntityId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected Entity(TEntityId id) => Id = id;

    public TEntityId Id { get; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Records a domain event to be published after the entity is persisted.
    /// </summary>
    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>
    /// Returns all raised events and clears the internal collection.
    /// Call this after a successful unit of work and dispatch the result.
    /// </summary>
    public IReadOnlyList<IDomainEvent> PullDomainEvents()
    {
        var events = _domainEvents.ToArray();
        _domainEvents.Clear();
        return events;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TEntityId> other || GetType() != other.GetType())
        {
            return false;
        }

        return Id.Equals(other.Id);
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TEntityId>? left, Entity<TEntityId>? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity<TEntityId>? left, Entity<TEntityId>? right)
        => !(left == right);
}
