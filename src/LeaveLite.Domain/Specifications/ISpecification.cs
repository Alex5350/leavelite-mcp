namespace LeaveLite.Domain.Specifications;

/// <summary>A business rule that can be evaluated against a candidate.</summary>
public interface ISpecification<in T>
{
    bool IsSatisfiedBy(T candidate);
}
