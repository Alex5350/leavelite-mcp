using LeaveLite.Domain.Employees;
using LeaveLite.Domain.Holidays;
using LeaveLite.Domain.LeaveRequests;
using LeaveLite.Domain.Policies;

namespace LeaveLite.Domain.Balances;

/// <summary>
/// Pure accrual computation. The as-of date is an input, not read from a clock, so
/// implementations must stay deterministic and side-effect free.
/// </summary>
public interface IBalanceCalculator
{
    /// <summary>
    /// Computes <see cref="Balance"/> for <paramref name="employee"/> under <paramref name="policy"/>
    /// as of <paramref name="asOf"/>.
    /// <para>
    /// Accrued: zero before the tenure gate (<see cref="AccrualPolicy.MinTenureMonths"/> since hire and
    /// matching employment type); otherwise periods elapsed since hire x <see cref="AccrualPolicy.HoursPerPeriod"/>
    /// — Monthly accrual is prorated by the exact fraction of the current period elapsed, capped by
    /// <see cref="AccrualPolicy.AnnualCapHours"/> — or, when <see cref="AccrualPolicy.GrantsBalanceUpfront"/> is set,
    /// the full annual amount once eligible.
    /// </para>
    /// <para>
    /// Consumed: approved leave of the policy's <see cref="Enums.LeaveType"/> only, counted as working days
    /// (Mon-Fri minus holidays/weekends) x <see cref="Common.WorkSchedule.StandardHoursPerDay"/>. Every approved
    /// request passed in is counted, including future-dated ones — callers decide the cutoff.
    /// </para>
    /// </summary>
    Balance Calculate(
        Employee employee,
        AccrualPolicy policy,
        DateOnly asOf,
        IReadOnlyCollection<LeaveRequest> historicalApprovedLeave,
        HolidayCalendar? holidays);
}
