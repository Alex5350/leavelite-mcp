namespace LeaveLite.Domain.Specifications;

/// <summary>Convenience base class for specifications. Combine via the And/Or/Not extension members.</summary>
public abstract class Specification<T> : ISpecification<T>
{
    public abstract bool IsSatisfiedBy(T candidate);
}

internal sealed class AndSpecification<T>(ISpecification<T> left, ISpecification<T> right) : Specification<T>
{
    public override bool IsSatisfiedBy(T candidate)
        => left.IsSatisfiedBy(candidate) && right.IsSatisfiedBy(candidate);
}

internal sealed class OrSpecification<T>(ISpecification<T> left, ISpecification<T> right) : Specification<T>
{
    public override bool IsSatisfiedBy(T candidate)
        => left.IsSatisfiedBy(candidate) || right.IsSatisfiedBy(candidate);
}

internal sealed class NotSpecification<T>(ISpecification<T> inner) : Specification<T>
{
    public override bool IsSatisfiedBy(T candidate) => !inner.IsSatisfiedBy(candidate);
}
