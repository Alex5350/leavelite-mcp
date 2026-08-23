using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Errors;
using LeaveLite.Domain.Policies;

namespace LeaveLite.Domain.UnitTests.Policies;

public sealed class AccrualPolicyTests
{
    [Fact]
    public void Create_ValidPolicy_SetsAllPropertiesAndTrimsName()
    {
        var result = AccrualPolicy.Create(
            "  Vacation Monthly  ",
            LeaveType.Vacation,
            EmploymentType.FullTime,
            AccrualPeriod.Monthly,
            hoursPerPeriod: 16m,
            annualCapHours: 192m,
            carryOverCapHours: 40m,
            minTenureMonths: 3,
            grantsBalanceUpfront: false);

        Assert.False(result.IsError);
        var policy = result.Value;
        Assert.Equal("Vacation Monthly", policy.Name);
        Assert.Equal(LeaveType.Vacation, policy.LeaveType);
        Assert.Equal(EmploymentType.FullTime, policy.EmploymentType);
        Assert.Equal(AccrualPeriod.Monthly, policy.AccrualPeriod);
        Assert.Equal(16m, policy.HoursPerPeriod);
        Assert.Equal(192m, policy.AnnualCapHours);
        Assert.Equal(40m, policy.CarryOverCapHours);
        Assert.Equal(3, policy.MinTenureMonths);
        Assert.False(policy.GrantsBalanceUpfront);
        Assert.NotEqual(default, policy.Id);
    }

    [Fact]
    public void Create_EmptyName_ReturnsNameRequired()
    {
        var result = AccrualPolicy.Create(
            "   ",
            LeaveType.Vacation,
            EmploymentType.FullTime,
            AccrualPeriod.Monthly,
            16m, null, null, 0, false);

        Assert.True(result.IsError);
        Assert.Equal(AccrualPolicyErrors.NameRequired.Code, result.FirstError.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_NonPositiveHoursPerPeriod_ReturnsHoursPerPeriodNotPositive(decimal hours)
    {
        var result = AccrualPolicy.Create(
            "Vacation Monthly",
            LeaveType.Vacation,
            EmploymentType.FullTime,
            AccrualPeriod.Monthly,
            hours, null, null, 0, false);

        Assert.True(result.IsError);
        Assert.Equal(AccrualPolicyErrors.HoursPerPeriodNotPositive(hours).Code, result.FirstError.Code);
    }

    [Fact]
    public void Create_AnnualCapBelowPerPeriodAccrual_ReturnsAnnualCapBelowAccrual()
    {
        var result = AccrualPolicy.Create(
            "Vacation Monthly",
            LeaveType.Vacation,
            EmploymentType.FullTime,
            AccrualPeriod.Monthly,
            16m,
            annualCapHours: 15.9m,
            carryOverCapHours: null,
            minTenureMonths: 0,
            grantsBalanceUpfront: false);

        Assert.True(result.IsError);
        Assert.Equal(AccrualPolicyErrors.AnnualCapBelowAccrual(15.9m, 16m).Code, result.FirstError.Code);
    }

    [Fact]
    public void Create_CarryOverCapBelowPerPeriodAccrual_ReturnsCarryOverCapBelowAccrual()
    {
        var result = AccrualPolicy.Create(
            "Vacation Monthly",
            LeaveType.Vacation,
            EmploymentType.FullTime,
            AccrualPeriod.Monthly,
            16m,
            annualCapHours: null,
            carryOverCapHours: 4m,
            minTenureMonths: 0,
            grantsBalanceUpfront: false);

        Assert.True(result.IsError);
        Assert.Equal(AccrualPolicyErrors.CarryOverCapBelowAccrual(4m, 16m).Code, result.FirstError.Code);
    }

    [Fact]
    public void Create_CapsExactlyEqualToPerPeriodAccrual_AreAllowed()
    {
        var result = AccrualPolicy.Create(
            "Tight Policy",
            LeaveType.Sick,
            EmploymentType.FullTime,
            AccrualPeriod.Yearly,
            40m,
            annualCapHours: 40m,
            carryOverCapHours: 40m,
            minTenureMonths: 0,
            grantsBalanceUpfront: false);

        Assert.False(result.IsError);
        Assert.Equal(40m, result.Value.AnnualCapHours);
        Assert.Equal(40m, result.Value.CarryOverCapHours);
    }

    [Fact]
    public void Create_NegativeMinTenureMonths_ReturnsMinTenureMonthsNegative()
    {
        var result = AccrualPolicy.Create(
            "Vacation Monthly",
            LeaveType.Vacation,
            EmploymentType.FullTime,
            AccrualPeriod.Monthly,
            16m, null, null,
            minTenureMonths: -1,
            grantsBalanceUpfront: false);

        Assert.True(result.IsError);
        Assert.Equal(AccrualPolicyErrors.MinTenureMonthsNegative.Code, result.FirstError.Code);
    }

    [Fact]
    public void Create_MultipleInvariantViolations_ReturnsAllErrors()
    {
        var result = AccrualPolicy.Create(
            "",
            LeaveType.Vacation,
            EmploymentType.FullTime,
            AccrualPeriod.Monthly,
            16m,
            annualCapHours: -1m,
            carryOverCapHours: -1m,
            minTenureMonths: -5,
            grantsBalanceUpfront: false);

        Assert.True(result.IsError);
        // 4 errors: NameRequired, AnnualCapBelowAccrual, CarryOverCapBelowAccrual, MinTenureMonthsNegative.
        // HoursPerPeriod (16) is positive, so HoursPerPeriodNotPositive does not fire.
        Assert.Equal(4, result.Errors.Count);
    }

    [Theory]
    [InlineData(AccrualPeriod.Monthly, 12)]
    [InlineData(AccrualPeriod.Yearly, 1)]
    public void PeriodsPerYear_MatchesAccrualPeriod(AccrualPeriod period, int expected)
    {
        var policy = AccrualPolicy.Create(
            "P",
            LeaveType.Vacation,
            EmploymentType.FullTime,
            period,
            10m, null, null, 0, false).Value;

        Assert.Equal(expected, policy.PeriodsPerYear);
    }

    [Fact]
    public void AnnualAmount_MultipliesHoursPerPeriodByPeriodsPerYear()
    {
        var monthly = AccrualPolicy.Create(
            "M", LeaveType.Vacation, EmploymentType.FullTime, AccrualPeriod.Monthly,
            16m, null, null, 0, false).Value;
        var yearly = AccrualPolicy.Create(
            "Y", LeaveType.Parental, EmploymentType.FullTime, AccrualPeriod.Yearly,
            160m, null, null, 0, true).Value;

        Assert.Equal(192m, monthly.AnnualAmount);
        Assert.Equal(160m, yearly.AnnualAmount);
    }

    [Fact]
    public void IsEligibleEmployment_MatchesOnlyTheConfiguredEmploymentType()
    {
        var policy = AccrualPolicy.Create(
            "Contractor Sick",
            LeaveType.Sick,
            EmploymentType.Contractor,
            AccrualPeriod.Yearly,
            24m, null, null, 0, true).Value;

        Assert.True(policy.IsEligibleEmployment(EmploymentType.Contractor));
        Assert.False(policy.IsEligibleEmployment(EmploymentType.FullTime));
        Assert.False(policy.IsEligibleEmployment(EmploymentType.PartTime));
    }
}
