using ErrorOr;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.Errors;

public static class LeaveRequestErrors
{
    public static Error NotFound(LeaveRequestId requestId)
        => Error.NotFound("LeaveRequest.NotFound", $"Leave request '{requestId}' was not found.");

    public static Error InvalidEmployeeId
        => Error.Validation("LeaveRequest.InvalidEmployeeId", "A non-empty employee id is required.");

    public static Error InvalidLeaveType
        => Error.Validation("LeaveRequest.InvalidLeaveType", "Leave type must be a defined value.");

    public static Error InvalidReasonLength
        => Error.Validation("LeaveRequest.InvalidReasonLength", "Reason must be 2000 characters or fewer.");

    public static Error StartDateInPast(DateOnly start, DateOnly today)
        => Error.Validation(
            "LeaveRequest.StartDateInPast",
            $"Leave start {start:O} must be today ({today:O}) or later; retroactive requests are not allowed.");

    public static Error OverlappingRequest(DateOnly start, DateOnly end)
        => Error.Conflict(
            "LeaveRequest.OverlappingRequest",
            $"An existing pending or approved request already overlaps {start:O}..{end:O}.");

    /// <summary>Includes the shortfall so callers can surface the exact deficit hours.</summary>
    public static Error InsufficientBalance(decimal requestedHours, decimal availableHours, decimal deficitHours)
        => Error.Conflict(
            "LeaveRequest.InsufficientBalance",
            $"Insufficient balance: requested {requestedHours}h, available {availableHours}h, deficit {deficitHours}h.");

    public static Error AlreadyDecided(RequestStatus currentStatus)
        => Error.Conflict(
            "LeaveRequest.AlreadyDecided",
            $"Request is already {currentStatus}; only pending requests can be decided.");

    public static Error DenialReasonRequired
        => Error.Validation("LeaveRequest.DenialReasonRequired", "A denial reason is required when denying a request.");

    public static Error InvalidApprover
        => Error.Validation("LeaveRequest.InvalidApprover", "A non-empty approver id is required.");

    public static Error ApproverNotTeamManager
        => Error.Forbidden(
            "LeaveRequest.ApproverNotTeamManager",
            "Only a Manager of the same team as the requesting employee may decide this request.");

    public static Error NotOwner
        => Error.Forbidden("LeaveRequest.NotOwner", "Only the employee who submitted the request may cancel it.");

    public static Error CannotCancelStarted(DateOnly start)
        => Error.Conflict(
            "LeaveRequest.CannotCancelStarted",
            $"Approved leave starting {start:O} has already started or started in the past and can no longer be cancelled.");

    public static Error MinimumStaffingNotMet(int minimumStaff, DateOnly start)
        => Error.Conflict(
            "LeaveRequest.MinimumStaffingNotMet",
            $"Approving would leave fewer than {minimumStaff} team member(s) available on some working day of {start:O} onwards.");
}
