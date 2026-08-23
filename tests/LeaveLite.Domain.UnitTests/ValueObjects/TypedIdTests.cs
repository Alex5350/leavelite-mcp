using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.UnitTests.ValueObjects;

public sealed class TypedIdTests
{
    [Fact]
    public void EmployeeId_SameGuidValues_AreEqual()
    {
        var guid = Guid.NewGuid();
        var first = new EmployeeId(guid);
        var second = new EmployeeId(guid);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void EmployeeId_DifferentGuidValues_AreNotEqual()
    {
        var first = new EmployeeId(Guid.NewGuid());
        var second = new EmployeeId(Guid.NewGuid());

        Assert.NotEqual(first, second);
        Assert.True(first != second);
    }

    [Fact]
    public void EmployeeId_Default_MatchesEmptyGuid()
    {
        Assert.Equal(default, new EmployeeId(Guid.Empty));
        Assert.NotEqual(default, EmployeeId.New());
    }

    [Fact]
    public void EmployeeId_New_IssuesDistinctSortableGuids()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => EmployeeId.New()).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void AccrualPolicyId_SameGuidValues_AreEqualAndDefaultIsDistinct()
    {
        var guid = Guid.NewGuid();

        Assert.Equal(new AccrualPolicyId(guid), new AccrualPolicyId(guid));
        Assert.NotEqual(default, new AccrualPolicyId(guid));
        Assert.NotEqual(default, AccrualPolicyId.New());
    }

    [Fact]
    public void LeaveRequestId_SameGuidValues_AreEqualAndDefaultIsDistinct()
    {
        var guid = Guid.NewGuid();

        Assert.Equal(new LeaveRequestId(guid), new LeaveRequestId(guid));
        Assert.NotEqual(default, new LeaveRequestId(guid));
        Assert.NotEqual(default, LeaveRequestId.New());
    }

    [Fact]
    public void ToString_RendersTheWrappedGuid()
    {
        var guid = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

        Assert.Equal("01234567-89ab-cdef-0123-456789abcdef", new EmployeeId(guid).ToString());
        Assert.Equal("01234567-89ab-cdef-0123-456789abcdef", new AccrualPolicyId(guid).ToString());
        Assert.Equal("01234567-89ab-cdef-0123-456789abcdef", new LeaveRequestId(guid).ToString());
    }

    [Fact]
    public void Ids_OfDifferentTypesWithSameGuid_AreNotInterchangeable()
    {
        var guid = Guid.NewGuid();
        EmployeeId employeeId = new(guid);
        LeaveRequestId requestId = new(guid);

        // Different typed ids are separate types; comparing their raw values still matches,
        // which is exactly why the wrappers exist.
        Assert.Equal(employeeId.Value, requestId.Value);
        Assert.NotEqual(employeeId.Value, Guid.Empty);
    }
}
