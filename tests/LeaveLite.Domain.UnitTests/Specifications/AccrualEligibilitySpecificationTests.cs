using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Specifications;

namespace LeaveLite.Domain.UnitTests.Specifications;

public sealed class AccrualEligibilitySpecificationTests
{
    private readonly AccrualEligibilitySpecification _specification = new();

    private bool IsEligible(Domain.Employees.Employee employee, Domain.Policies.AccrualPolicy policy, DateOnly asOf)
        => _specification.IsSatisfiedBy(new AccrualEligibilityContext(employee, policy, asOf));

    [Fact]
    public void MatchingEmploymentTypeAndReachedTenure_IsSatisfied()
    {
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 15));
        var policy = TestPolicies.MonthlyVacation(minTenureMonths: 3);

        Assert.True(IsEligible(employee, policy, new DateOnly(2026, 5, 1)));
    }

    [Fact]
    public void OneDayBeforeTenureGate_IsNotSatisfied()
    {
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 15));
        var policy = TestPolicies.MonthlyVacation(minTenureMonths: 3); // gate: 2026-04-15

        Assert.False(IsEligible(employee, policy, new DateOnly(2026, 4, 14)));
    }

    [Fact]
    public void ExactlyOnTenureGate_IsSatisfied()
    {
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 15));
        var policy = TestPolicies.MonthlyVacation(minTenureMonths: 3);

        Assert.True(IsEligible(employee, policy, new DateOnly(2026, 4, 15)));
    }

    [Fact]
    public void ZeroTenureGate_IsSatisfiedOnTheHireDayItself()
    {
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 15));
        var policy = TestPolicies.MonthlyVacation(minTenureMonths: 0);

        Assert.True(IsEligible(employee, policy, new DateOnly(2026, 1, 15)));
    }

    [Fact]
    public void EmploymentTypeMismatch_IsNotSatisfied()
    {
        var partTimer = TestEmployees.Standard(new DateOnly(2020, 1, 1), EmploymentType.PartTime);
        var fullTimePolicy = TestPolicies.MonthlyVacation(minTenureMonths: 0);

        Assert.False(IsEligible(partTimer, fullTimePolicy, new DateOnly(2026, 8, 22)));
    }

    [Fact]
    public void ContractorEmployee_OnContractorPolicy_IsSatisfied()
    {
        var contractor = TestEmployees.Standard(new DateOnly(2026, 1, 15), EmploymentType.Contractor);
        var contractorPolicy = TestPolicies.Yearly(LeaveType.Sick, 24m, employmentType: EmploymentType.Contractor);

        Assert.True(IsEligible(contractor, contractorPolicy, new DateOnly(2026, 8, 22)));
    }

    [Fact]
    public void TenureGate_ClampsMonthEndAnniversariesAcrossFebruary()
    {
        // Hired Nov 30, 2025; 3 months later clamps to Feb 28, 2026 (2026 is not a leap year).
        var employee = TestEmployees.Standard(new DateOnly(2025, 11, 30));
        var policy = TestPolicies.MonthlyVacation(minTenureMonths: 3);

        Assert.False(IsEligible(employee, policy, new DateOnly(2026, 2, 27)));
        Assert.True(IsEligible(employee, policy, new DateOnly(2026, 2, 28)));
    }
}
