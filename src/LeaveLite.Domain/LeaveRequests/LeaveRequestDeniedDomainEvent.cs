using LeaveLite.Domain.Common;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.LeaveRequests;

/// <summary>Raised when a pending leave request is denied.</summary>
public sealed record LeaveRequestDeniedDomainEvent(
    LeaveRequestId RequestId,
    EmployeeId EmployeeId,
    EmployeeId ApproverId,
    string DenialReason,
    DateTimeOffset DecidedAtUtc) : IDomainEvent;
