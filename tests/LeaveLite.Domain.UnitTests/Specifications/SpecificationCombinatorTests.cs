using LeaveLite.Domain.Specifications;

namespace LeaveLite.Domain.UnitTests.Specifications;

public sealed class SpecificationCombinatorTests
{
    private sealed class PredicateSpecification<T>(Func<T, bool> predicate) : Specification<T>
    {
        public override bool IsSatisfiedBy(T candidate) => predicate(candidate);
    }

    private static readonly ISpecification<bool> True = new PredicateSpecification<bool>(_ => true);

    private static readonly ISpecification<bool> False = new PredicateSpecification<bool>(_ => false);

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void And_IsSatisfiedOnlyWhenBothSidesAre(bool left, bool right, bool expected)
    {
        var combined = Spec(left).And(Spec(right));

        Assert.Equal(expected, combined.IsSatisfiedBy(true));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void Or_IsSatisfiedWhenEitherSideIs(bool left, bool right, bool expected)
    {
        var combined = Spec(left).Or(Spec(right));

        Assert.Equal(expected, combined.IsSatisfiedBy(true));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Not_InvertsTheInnerSpecification(bool inner, bool expected)
    {
        var combined = Spec(inner).Not();

        Assert.Equal(expected, combined.IsSatisfiedBy(true));
    }

    [Fact]
    public void Combinators_ComposeIntoLargerRules()
    {
        // True AND (False OR Not(False)) == True AND True == True
        var rule = True.And(False.Or(False.Not()));

        Assert.True(rule.IsSatisfiedBy(true));
        Assert.False(rule.Not().IsSatisfiedBy(true));
    }

    [Fact]
    public void CombinedSpecifications_AreSpecificationsAgain()
    {
        ISpecification<bool> combined = True.And(True).Or(False);

        Assert.IsAssignableFrom<ISpecification<bool>>(combined);
        Assert.True(combined.IsSatisfiedBy(true));
    }

    private static ISpecification<bool> Spec(bool result)
        => result ? True : False;
}
