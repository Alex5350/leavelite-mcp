using LeaveLite.Domain.Enums;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.Balances;

/// <summary>
/// The result of an accrual computation. <see cref="BalanceHours"/> is NOT floored at zero:
/// a negative balance is a real signal that approved leave exceeds accrued hours.
/// </summary>
public sealed record Balance(
    EmployeeId EmployeeId,
    LeaveType LeaveType,
    DateOnly AsOf,
    decimal AccruedHours,
    decimal ConsumedHours,
    decimal BalanceHours)
{
    /// <summary>True when the employee has committed more leave hours than accrued.</summary>
    public bool IsOverdrawn => BalanceHours < 0;
}
