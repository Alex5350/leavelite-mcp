using LeaveLite.Domain.Balances;
using LeaveLite.Domain.Common;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Holidays;
using LeaveLite.Domain.LeaveRequests;

namespace LeaveLite.Domain.UnitTests.Balances;

public sealed class AccrualBalanceCalculatorTests
{
    private readonly AccrualBalanceCalculator _calculator = new();

    private static readonly HolidayCalendar Us2026 = HolidayCalendar.Create(
        2026,
        [new Holiday(new DateOnly(2026, 9, 7), "Labor Day"), new Holiday(new DateOnly(2026, 11, 26), "Thanksgiving Day")]);

    // 2026-09-14..18 is a Mon-Fri week with no holidays; 2026-09-07..11 is Mon-Fri with Labor Day on the Monday.
    private static readonly DateOnly PlainWeekStart = new(2026, 9, 14);
    private static readonly DateOnly PlainWeekEnd = new(2026, 9, 18);
    private static readonly DateOnly HolidayWeekStart = new(2026, 9, 7);
    private static readonly DateOnly HolidayWeekEnd = new(2026, 9, 11);

    private Balance CalculateFor(
        Domain.Employees.Employee employee,
        Domain.Policies.AccrualPolicy policy,
        DateOnly asOf,
        IReadOnlyCollection<LeaveRequest> history,
        HolidayCalendar? holidays = null)
        => _calculator.Calculate(employee, policy, asOf, history, holidays);

    [Fact]
    public void Calculate_BeforeHireDate_AccruesNothing()
    {
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 15));
        var policy = TestPolicies.MonthlyVacation(minTenureMonths: 0);

        var balance = CalculateFor(employee, policy, new DateOnly(2026, 1, 14), []);

        Assert.Equal(0m, balance.AccruedHours);
        Assert.Equal(0m, balance.ConsumedHours);
        Assert.Equal(0m, balance.BalanceHours);
    }

    [Fact]
    public void Calculate_BeforeTenureGate_AccruesNothing()
    {
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 15));
        var policy = TestPolicies.MonthlyVacation(minTenureMonths: 3); // gate: 2026-04-15

        var beforeGate = CalculateFor(employee, policy, new DateOnly(2026, 4, 14), []);
        var onGateDay = CalculateFor(employee, policy, new DateOnly(2026, 4, 15), []);

        Assert.Equal(0m, beforeGate.AccruedHours);
        Assert.True(onGateDay.AccruedHours > 0m);
    }

    [Fact]
    public void Calculate_EmploymentTypeMismatch_AccruesNothing()
    {
        var partTimer = TestEmployees.Standard(new DateOnly(2024, 1, 1), EmploymentType.PartTime);
        var fullTimePolicy = TestPolicies.MonthlyVacation(minTenureMonths: 0);

        var balance = CalculateFor(partTimer, fullTimePolicy, new DateOnly(2026, 8, 22), []);

        Assert.Equal(0m, balance.AccruedHours);
    }

    [Fact]
    public void Calculate_ContractorEmployee_OnContractorPolicy_AccruesNormally()
    {
        var contractor = TestEmployees.Standard(new DateOnly(2026, 1, 15), EmploymentType.Contractor);
        var contractorPolicy = TestPolicies.Yearly(LeaveType.Sick, 24m, grantsBalanceUpfront: true, employmentType: EmploymentType.Contractor);

        var balance = CalculateFor(contractor, contractorPolicy, new DateOnly(2026, 2, 1), []);

        Assert.Equal(24m, balance.AccruedHours);
    }

    [Fact]
    public void Calculate_MonthlyAccrual_MidPeriodIsProratedToTwoDecimals()
    {
        // Hired 2026-01-15; as of 2026-03-20: 2 whole months plus 6 of the 31 days of the
        // current anchored period (Mar 15 - Apr 15), accrues through the as-of day:
        // (2 + 6/31) * 16 = 35.0967... -> 35.10 (rounded half away from zero to 2dp).
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 15));
        var policy = TestPolicies.MonthlyVacation(hoursPerPeriod: 16m, minTenureMonths: 0);

        var balance = CalculateFor(employee, policy, new DateOnly(2026, 3, 20), []);

        Assert.Equal(35.10m, balance.AccruedHours);
    }

    [Fact]
    public void Calculate_MonthlyAccrual_AccruesThroughTheAsOfDay()
    {
        // Exactly two whole months after hire, the first day of the third period has already
        // accrued one day's fraction: (2 + 1/31) * 16 = 32.5161... -> 32.52.
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 15));
        var policy = TestPolicies.MonthlyVacation(hoursPerPeriod: 16m, minTenureMonths: 0);

        var balance = CalculateFor(employee, policy, new DateOnly(2026, 3, 15), []);

        Assert.Equal(32.52m, balance.AccruedHours);
    }

    [Fact]
    public void Calculate_MonthlyAccrual_TenureGateBackfillsAccrualOnceEligible()
    {
        // Accrual is anchored at the hire date, not at the tenure gate: on the gate day the
        // employee is credited the gated months as well — 3 months + 1 day of the 30-day
        // anchored period Apr 15 - May 15: (3 + 1/30) * 16 = 48.5333... -> 48.53.
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 15));
        var policy = TestPolicies.MonthlyVacation(hoursPerPeriod: 16m, minTenureMonths: 3);

        var balance = CalculateFor(employee, policy, new DateOnly(2026, 4, 15), []);

        Assert.Equal(48.53m, balance.AccruedHours);
    }

    [Fact]
    public void Calculate_MonthlyAccrual_IsCappedAtAnnualCap()
    {
        var employee = TestEmployees.Standard(new DateOnly(2020, 1, 1));
        var policy = TestPolicies.MonthlyVacation(hoursPerPeriod: 16m, minTenureMonths: 0, annualCapHours: 100m);

        var balance = CalculateFor(employee, policy, new DateOnly(2026, 1, 1), []);

        Assert.Equal(100m, balance.AccruedHours);
    }

    [Fact]
    public void Calculate_MonthlyAccrual_BelowCap_IsNotInflated()
    {
        var employee = TestEmployees.Standard(new DateOnly(2026, 5, 1));
        var policy = TestPolicies.MonthlyVacation(hoursPerPeriod: 16m, minTenureMonths: 0, annualCapHours: 192m);

        var balance = CalculateFor(employee, policy, new DateOnly(2026, 5, 20), []);

        // (0 + 20/31) * 16 = 10.322... -> 10.32.
        Assert.Equal(10.32m, balance.AccruedHours);
    }

    [Fact]
    public void Calculate_YearlyUpfront_GrantsFullAnnualAmountAtEligibility()
    {
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 5));
        var policy = TestPolicies.Yearly(LeaveType.Sick, 40m, minTenureMonths: 0, grantsBalanceUpfront: true);

        var balance = CalculateFor(employee, policy, new DateOnly(2026, 1, 5), []);

        Assert.Equal(40m, balance.AccruedHours);
    }

    [Fact]
    public void Calculate_YearlyUpfront_BeforeTenureGate_GrantsNothing()
    {
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 5));
        var policy = TestPolicies.Yearly(LeaveType.Sick, 40m, minTenureMonths: 3, grantsBalanceUpfront: true);

        var beforeGate = CalculateFor(employee, policy, new DateOnly(2026, 4, 4), []);
        var onGateDay = CalculateFor(employee, policy, new DateOnly(2026, 4, 5), []);

        Assert.Equal(0m, beforeGate.AccruedHours);
        Assert.Equal(40m, onGateDay.AccruedHours);
    }

    [Fact]
    public void Calculate_YearlyUpfront_IsStillBoundedByTheAnnualCap()
    {
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 5));
        // A monthly policy granting its 192h annual amount upfront but capped at 100h/year.
        var policy = Domain.Policies.AccrualPolicy.Create(
            "Vacation Upfront Capped",
            LeaveType.Vacation,
            EmploymentType.FullTime,
            AccrualPeriod.Monthly,
            hoursPerPeriod: 16m,
            annualCapHours: 100m,
            carryOverCapHours: null,
            minTenureMonths: 0,
            grantsBalanceUpfront: true).Value;

        var balance = CalculateFor(employee, policy, new DateOnly(2026, 2, 1), []);

        Assert.Equal(100m, balance.AccruedHours);
    }

    [Theory]
    [InlineData("2026-06-14", 160)] // one day before the second anniversary
    [InlineData("2026-06-15", 320)] // exactly two years
    [InlineData("2027-06-15", 480)] // three years
    public void Calculate_YearlyNonUpfront_CountsWholeYearsOnly(string asOf, int expected)
    {
        var employee = TestEmployees.Standard(new DateOnly(2024, 6, 15));
        var policy = TestPolicies.Yearly(LeaveType.Vacation, 160m, minTenureMonths: 0, grantsBalanceUpfront: false);

        var balance = CalculateFor(employee, policy, DateOnly.Parse(asOf), []);

        Assert.Equal(expected, balance.AccruedHours);
    }

    [Fact]
    public void Calculate_Consumed_CountsApprovedWorkingDaysTimesEightHours()
    {
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 5));
        var policy = TestPolicies.MonthlyVacation(minTenureMonths: 0);
        var approved = TestRequests.Approved(employee.Id, PlainWeekStart, PlainWeekEnd);

        var balance = CalculateFor(employee, policy, new DateOnly(2026, 9, 30), [approved]);

        Assert.Equal(40m, balance.ConsumedHours); // 5 working days * 8h
    }

    [Fact]
    public void Calculate_Consumed_ExcludesHolidaysInsideApprovedLeave()
    {
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 5));
        var policy = TestPolicies.MonthlyVacation(minTenureMonths: 0);
        var approved = TestRequests.Approved(employee.Id, HolidayWeekStart, HolidayWeekEnd);

        var balance = CalculateFor(employee, policy, new DateOnly(2026, 9, 30), [approved], Us2026);

        Assert.Equal(32m, balance.ConsumedHours); // Mon-Fri minus Labor Day Monday
    }

    [Fact]
    public void Calculate_Consumed_WithoutCalendar_ExcludesWeekendsOnly()
    {
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 5));
        var policy = TestPolicies.MonthlyVacation(minTenureMonths: 0);
        var approved = TestRequests.Approved(employee.Id, HolidayWeekStart, HolidayWeekEnd);

        var balance = CalculateFor(employee, policy, new DateOnly(2026, 9, 30), [approved], holidays: null);

        Assert.Equal(40m, balance.ConsumedHours); // Labor Day is not a holiday without a calendar
    }

    [Fact]
    public void Calculate_Consumed_IgnoresPendingDeniedAndCancelledRequests()
    {
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 5));
        var policy = TestPolicies.MonthlyVacation(minTenureMonths: 0);
        var requests = new[]
        {
            TestRequests.Pending(employee.Id, PlainWeekStart, PlainWeekEnd),
            TestRequests.Denied(employee.Id, PlainWeekStart, PlainWeekEnd),
            TestRequests.Cancelled(employee.Id, PlainWeekStart, PlainWeekEnd),
        };

        var balance = CalculateFor(employee, policy, new DateOnly(2026, 9, 30), requests);

        Assert.Equal(0m, balance.ConsumedHours);
    }

    [Fact]
    public void Calculate_Consumed_CountsOnlyThePolicyLeaveType()
    {
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 5));
        var vacation = TestPolicies.MonthlyVacation(minTenureMonths: 0);
        var sick = TestPolicies.Yearly(LeaveType.Sick, 40m, minTenureMonths: 0, grantsBalanceUpfront: true);
        var approvedVacation = TestRequests.Approved(employee.Id, PlainWeekStart, PlainWeekEnd, LeaveType.Vacation);
        var approvedSick = TestRequests.Approved(employee.Id, PlainWeekStart, PlainWeekEnd, LeaveType.Sick);

        var vacationBalance = CalculateFor(employee, vacation, new DateOnly(2026, 9, 30), [approvedVacation, approvedSick]);
        var sickBalance = CalculateFor(employee, sick, new DateOnly(2026, 9, 30), [approvedVacation, approvedSick]);

        Assert.Equal(40m, vacationBalance.ConsumedHours);
        Assert.Equal(40m, sickBalance.ConsumedHours); // same week charged once per leave type
    }

    [Fact]
    public void Calculate_Consumed_SumsMultipleApprovedRequests()
    {
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 5));
        var policy = TestPolicies.MonthlyVacation(minTenureMonths: 0);
        var first = TestRequests.Approved(employee.Id, PlainWeekStart, PlainWeekEnd);
        var second = TestRequests.Approved(employee.Id, HolidayWeekStart, HolidayWeekEnd);

        var balance = CalculateFor(employee, policy, new DateOnly(2026, 9, 30), [first, second], Us2026);

        Assert.Equal(40m + 32m, balance.ConsumedHours);
    }

    [Fact]
    public void Calculate_IneligibleEmployeeWithApprovedLeave_GoesNegativeAndIsFlaggedOverdrawn()
    {
        var employee = TestEmployees.Standard(new DateOnly(2026, 7, 1));
        var policy = TestPolicies.MonthlyVacation(minTenureMonths: 6); // gate not reached in 2026
        var approvedBeforeEligibility = TestRequests.Approved(employee.Id, PlainWeekStart, PlainWeekEnd);

        var balance = CalculateFor(employee, policy, new DateOnly(2026, 9, 30), [approvedBeforeEligibility]);

        Assert.Equal(0m, balance.AccruedHours);
        Assert.Equal(40m, balance.ConsumedHours);
        Assert.Equal(-40m, balance.BalanceHours);
        Assert.True(balance.IsOverdrawn);
    }

    [Fact]
    public void Calculate_ReturnsFullyPopulatedBalanceRecord()
    {
        var employee = TestEmployees.Standard(new DateOnly(2026, 1, 5));
        var policy = TestPolicies.MonthlyVacation(hoursPerPeriod: 16m, minTenureMonths: 0);
        var asOf = new DateOnly(2026, 3, 1);
        var approved = TestRequests.Approved(employee.Id, new DateOnly(2026, 3, 4), new DateOnly(2026, 3, 4)); // one Wednesday

        var balance = CalculateFor(employee, policy, asOf, [approved], Us2026);

        Assert.Equal(employee.Id, balance.EmployeeId);
        Assert.Equal(LeaveType.Vacation, balance.LeaveType);
        Assert.Equal(asOf, balance.AsOf);
        Assert.Equal(30.29m, balance.AccruedHours); // (1 + 25/28) * 16
        Assert.Equal(8m, balance.ConsumedHours);
        Assert.Equal(balance.AccruedHours - balance.ConsumedHours, balance.BalanceHours);
        Assert.False(balance.IsOverdrawn);
    }

    [Fact]
    public void WorkSchedule_StandardHoursPerDay_IsEight()
    {
        Assert.Equal(8m, WorkSchedule.StandardHoursPerDay);
    }
}
