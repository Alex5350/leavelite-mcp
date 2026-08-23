using LeaveLite.Domain.Common;
using LeaveLite.Domain.Employees;
using LeaveLite.Domain.Holidays;
using LeaveLite.Domain.LeaveRequests;
using LeaveLite.Domain.Policies;
using LeaveLite.Domain.Specifications;

namespace LeaveLite.Domain.Balances;

/// <summary>
/// Pure, deterministic accrual engine — no clock, no I/O, no state. Everything (including the
/// as-of date) arrives as an argument, making every rule below unit-testable in isolation.
/// </summary>
public sealed class AccrualBalanceCalculator : IBalanceCalculator
{
    private const int DecimalPlaces = 2;

    private static readonly AccrualEligibilitySpecification Eligibility = new();

    public Balance Calculate(
        Employee employee,
        AccrualPolicy policy,
        DateOnly asOf,
        IReadOnlyCollection<LeaveRequest> historicalApprovedLeave,
        HolidayCalendar? holidays)
    {
        var accrued = CalculateAccruedHours(employee, policy, asOf);
        var consumed = CalculateConsumedHours(policy, historicalApprovedLeave, holidays);

        return new Balance(employee.Id, policy.LeaveType, asOf, accrued, consumed, accrued - consumed);
    }

    /// <summary>
    /// Accrual anchored at the hire date. Before the tenure gate (or with a mismatched employment
    /// type) nothing accrues; consumed hours still apply, so an ineligible employee can be overdrawn.
    /// </summary>
    internal static decimal CalculateAccruedHours(Employee employee, AccrualPolicy policy, DateOnly asOf)
    {
        if (asOf < employee.HiredOn)
        {
            return 0m;
        }

        if (!Eligibility.IsSatisfiedBy(new AccrualEligibilityContext(employee, policy, asOf)))
        {
            return 0m;
        }

        if (policy.GrantsBalanceUpfront)
        {
            return ApplyAnnualCap(policy, Round(policy.AnnualAmount));
        }

        var accrued = policy.AccrualPeriod switch
        {
            Enums.AccrualPeriod.Monthly => CalculateMonthlyAccrual(employee.HiredOn, asOf, policy.HoursPerPeriod),
            Enums.AccrualPeriod.Yearly => CalculateYearlyAccrual(employee.HiredOn, asOf, policy.HoursPerPeriod),
            _ => 0m,
        };

        return ApplyAnnualCap(policy, Round(accrued));
    }

    /// <summary>
    /// Full months elapsed since hire plus the exact day-fraction of the current (anchored) period,
    /// multiplied by the per-period accrual.
    /// </summary>
    private static decimal CalculateMonthlyAccrual(DateOnly hiredOn, DateOnly asOf, decimal hoursPerPeriod)
    {
        var wholeMonths = CountWholeMonthsElapsed(hiredOn, asOf);

        var periodStart = hiredOn.AddMonths(wholeMonths);
        var periodEndExclusive = periodStart.AddMonths(1);
        var daysInPeriod = periodEndExclusive.DayNumber - periodStart.DayNumber;
        var elapsedDays = Math.Min(asOf.DayNumber - periodStart.DayNumber + 1, daysInPeriod); // +1: accrues through the as-of day
        var periodFraction = elapsedDays / (decimal)daysInPeriod;

        return (wholeMonths + periodFraction) * hoursPerPeriod;
    }

    /// <summary>Whole years elapsed since the hire anniversary — no proration for yearly policies.</summary>
    private static decimal CalculateYearlyAccrual(DateOnly hiredOn, DateOnly asOf, decimal hoursPerPeriod)
    {
        var years = asOf.Year - hiredOn.Year;
        if (hiredOn.AddYears(years) > asOf)
        {
            years--;
        }

        return Math.Max(years, 0) * hoursPerPeriod;
    }

    /// <summary>
    /// Approved leave of the policy's leave type only, as working days (Mon-Fri, holidays excluded)
    /// times <see cref="WorkSchedule.StandardHoursPerDay"/>.
    /// </summary>
    internal static decimal CalculateConsumedHours(
        AccrualPolicy policy,
        IReadOnlyCollection<LeaveRequest> historicalApprovedLeave,
        HolidayCalendar? holidays)
    {
        decimal consumed = 0m;

        foreach (var request in historicalApprovedLeave)
        {
            if (request.Status == Enums.RequestStatus.Approved && request.LeaveType == policy.LeaveType)
            {
                consumed += WorkSchedule.WorkingHours(request.DateRange, holidays);
            }
        }

        return consumed;
    }

    private static decimal ApplyAnnualCap(AccrualPolicy policy, decimal accrued)
        => policy.AnnualCapHours is { } cap ? Math.Min(accrued, cap) : accrued;

    private static decimal Round(decimal value)
        => decimal.Round(value, DecimalPlaces, MidpointRounding.AwayFromZero);

    private static int CountWholeMonthsElapsed(DateOnly from, DateOnly to)
    {
        var months = (to.Year - from.Year) * 12 + to.Month - from.Month;
        if (from.AddMonths(months) > to)
        {
            months--;
        }

        return Math.Max(months, 0);
    }
}
