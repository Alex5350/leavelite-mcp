namespace LeaveLite.Domain.Specifications;

/// <summary>Combinators for any <see cref="ISpecification{T}"/> (C# 14 extension members).</summary>
public static class SpecificationExtensions
{
    extension<T>(ISpecification<T> specification)
    {
        public ISpecification<T> And(ISpecification<T> other)
            => new AndSpecification<T>(specification, other);

        public ISpecification<T> Or(ISpecification<T> other)
            => new OrSpecification<T>(specification, other);

        public ISpecification<T> Not()
            => new NotSpecification<T>(specification);
    }
}
