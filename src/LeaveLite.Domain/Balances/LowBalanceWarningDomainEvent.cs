using LeaveLite.Domain.Common;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.Balances;

/// <summary>
/// Raised by the application layer (not by an aggregate) when a balance computation falls below
/// the low-balance threshold after new commitments.
/// </summary>
public sealed record LowBalanceWarningDomainEvent(
    EmployeeId EmployeeId,
    LeaveType LeaveType,
    decimal BalanceHours,
    decimal ThresholdHours,
    DateOnly AsOf) : IDomainEvent;
