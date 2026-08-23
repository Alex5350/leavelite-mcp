using ErrorOr;
using LeaveLite.Domain.Common;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Errors;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.LeaveRequests;

/// <summary>
/// Aggregate root representing one leave request. Illegal state transitions return typed errors
/// instead of throwing. Future-only dates are enforced by the application layer (which owns the clock);
/// this aggregate only enforces its own structural invariants.
/// </summary>
public sealed class LeaveRequest : Entity<LeaveRequestId>
{
    private const int ReasonMaxLength = 2000;

    private LeaveRequest(
        LeaveRequestId id,
        EmployeeId employeeId,
        LeaveType leaveType,
        DateRange dateRange,
        string? reason,
        DateTimeOffset submittedAtUtc)
        : base(id)
    {
        EmployeeId = employeeId;
        LeaveType = leaveType;
        DateRange = dateRange;
        Reason = reason;
        SubmittedAtUtc = submittedAtUtc;
        Status = RequestStatus.Pending;
    }

    public EmployeeId EmployeeId { get; }

    public LeaveType LeaveType { get; }

    public DateRange DateRange { get; }

    public string? Reason { get; }

    public RequestStatus Status { get; private set; }

    public DateTimeOffset SubmittedAtUtc { get; }

    public EmployeeId? DecidedBy { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public string? DenialReason { get; private set; }

    /// <summary>
    /// Creates a pending request with a fresh Guid v7 id. The caller supplies
    /// <paramref name="submittedAtUtc"/> from an injected clock.
    /// </summary>
    public static ErrorOr<LeaveRequest> Create(
        EmployeeId employeeId,
        LeaveType leaveType,
        DateRange dateRange,
        string? reason,
        DateTimeOffset submittedAtUtc)
    {
        List<Error> errors = [];

        if (employeeId == default)
        {
            errors.Add(LeaveRequestErrors.InvalidEmployeeId);
        }

        if (!Enum.IsDefined(leaveType))
        {
            errors.Add(LeaveRequestErrors.InvalidLeaveType);
        }

        if (reason?.Length > ReasonMaxLength)
        {
            errors.Add(LeaveRequestErrors.InvalidReasonLength);
        }

        if (submittedAtUtc == default)
        {
            errors.Add(Error.Validation("LeaveRequest.InvalidSubmittedAt", "Submission timestamp must be a real instant."));
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        return new LeaveRequest(LeaveRequestId.New(), employeeId, leaveType, dateRange, reason?.Trim(), submittedAtUtc);
    }

    /// <summary>Pending -> Approved. Raises <see cref="LeaveRequestApprovedDomainEvent"/>.</summary>
    public ErrorOr<Success> Approve(EmployeeId approverId, DateTimeOffset decidedAtUtc)
    {
        if (approverId == default)
        {
            return LeaveRequestErrors.InvalidApprover;
        }

        if (Status != RequestStatus.Pending)
        {
            return LeaveRequestErrors.AlreadyDecided(Status);
        }

        Status = RequestStatus.Approved;
        DecidedBy = approverId;
        DecidedAtUtc = decidedAtUtc;
        Raise(new LeaveRequestApprovedDomainEvent(Id, EmployeeId, approverId, DateRange, decidedAtUtc));

        return Result.Success;
    }

    /// <summary>Pending -> Denied. The denial reason is required. Raises <see cref="LeaveRequestDeniedDomainEvent"/>.</summary>
    public ErrorOr<Success> Deny(EmployeeId approverId, string? denialReason, DateTimeOffset decidedAtUtc)
    {
        if (approverId == default)
        {
            return LeaveRequestErrors.InvalidApprover;
        }

        if (string.IsNullOrWhiteSpace(denialReason))
        {
            return LeaveRequestErrors.DenialReasonRequired;
        }

        if (Status != RequestStatus.Pending)
        {
            return LeaveRequestErrors.AlreadyDecided(Status);
        }

        Status = RequestStatus.Denied;
        DecidedBy = approverId;
        DecidedAtUtc = decidedAtUtc;
        DenialReason = denialReason.Trim();
        Raise(new LeaveRequestDeniedDomainEvent(Id, EmployeeId, approverId, DenialReason, decidedAtUtc));

        return Result.Success;
    }

    /// <summary>
    /// Pending -> Cancelled (always allowed) or Approved -> Cancelled (only when the leave has not
    /// started yet, i.e. <paramref name="today"/> is before <see cref="DateRange"/>.Start).
    /// Any other transition returns a typed error.
    /// </summary>
    public ErrorOr<Success> Cancel(DateOnly today)
    {
        if (Status is RequestStatus.Denied or RequestStatus.Cancelled)
        {
            return LeaveRequestErrors.AlreadyDecided(Status);
        }

        if (Status == RequestStatus.Approved && today >= DateRange.Start)
        {
            return LeaveRequestErrors.CannotCancelStarted(DateRange.Start);
        }

        Status = RequestStatus.Cancelled;
        return Result.Success;
    }
}
