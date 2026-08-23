using LeaveLite.Domain.Common;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.LeaveRequests;

/// <summary>Raised when a pending leave request is approved.</summary>
public sealed record LeaveRequestApprovedDomainEvent(
    LeaveRequestId RequestId,
    EmployeeId EmployeeId,
    EmployeeId ApproverId,
    DateRange Period,
    DateTimeOffset DecidedAtUtc) : IDomainEvent;
