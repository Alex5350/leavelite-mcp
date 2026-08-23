using ErrorOr;
using FluentValidation;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Common;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Errors;
using LeaveLite.Domain.Specifications;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Application.LeaveRequests;

/// <summary>
/// Approves or denies a pending request. The approver must be a Manager in the same team as the
/// employee; approval additionally enforces the minimum-staffing specification over the request's
/// range using the team's other approved leave.
/// </summary>
public sealed record DecideLeaveRequestCommand(
    LeaveRequestId RequestId,
    EmployeeId ApproverId,
    bool Approve,
    string? DenialReason = null,
    int? MinimumStaff = null) : ICommand;

public sealed class DecideLeaveRequestValidator : AbstractValidator<DecideLeaveRequestCommand>
{
    public DecideLeaveRequestValidator()
    {
        RuleFor(command => command.RequestId).NotEqual(default(LeaveRequestId));
        RuleFor(command => command.ApproverId).NotEqual(default(EmployeeId));

        RuleFor(command => command.DenialReason)
            .NotEmpty()
            .MaximumLength(2000)
            .When(command => !command.Approve);

        RuleFor(command => command.MinimumStaff)
            .GreaterThanOrEqualTo(1)
            .When(command => command.MinimumStaff.HasValue);
    }
}

internal sealed class DecideLeaveRequestHandler(
    ILeaveRequestRepository leaveRequests,
    IEmployeeRepository employees,
    IHolidayCalendarRepository holidayCalendars,
    IDateTimeProvider time,
    IUnitOfWork unitOfWork,
    IDomainEventDispatcher dispatcher,
    IValidator<DecideLeaveRequestCommand> validator) : ICommandHandler<DecideLeaveRequestCommand>
{
    public async Task<ErrorOr<Success>> Handle(DecideLeaveRequestCommand command, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorsAsync(command, cancellationToken) is { } validationErrors)
        {
            return validationErrors;
        }

        if (await leaveRequests.GetByIdAsync(command.RequestId, cancellationToken) is not { } request)
        {
            return LeaveRequestErrors.NotFound(command.RequestId);
        }

        if (await employees.GetByIdAsync(command.ApproverId, cancellationToken) is not { } approver)
        {
            return EmployeeErrors.NotFound(command.ApproverId);
        }

        if (await employees.GetByIdAsync(request.EmployeeId, cancellationToken) is not { } employee)
        {
            return EmployeeErrors.NotFound(request.EmployeeId);
        }

        if (approver.TeamId != employee.TeamId || approver.TeamRole != TeamRole.Manager)
        {
            return LeaveRequestErrors.ApproverNotTeamManager;
        }

        // Fail fast on decided requests before spending effort on staffing checks;
        // the aggregate re-enforces the transition internally.
        if (request.Status != RequestStatus.Pending)
        {
            return LeaveRequestErrors.AlreadyDecided(request.Status);
        }

        if (command.Approve)
        {
            if (await CheckMinimumStaffingAsync(request, employee.TeamId, command.MinimumStaff, cancellationToken) is { } staffingError)
            {
                return staffingError;
            }

            var approved = request.Approve(approver.Id, time.UtcNow);
            if (approved.IsError)
            {
                return approved.Errors;
            }
        }
        else
        {
            var denied = request.Deny(approver.Id, command.DenialReason, time.UtcNow);
            if (denied.IsError)
            {
                return denied.Errors;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await dispatcher.DispatchAsync(request.PullDomainEvents(), cancellationToken);

        return Result.Success;
    }

    /// <summary>
    /// Builds the team coverage context (teammates excluding the requester, their approved leave
    /// overlapping the range, relevant holidays) and evaluates the staffing specification.
    /// </summary>
    private async Task<Error?> CheckMinimumStaffingAsync(
        Domain.LeaveRequests.LeaveRequest request,
        Guid teamId,
        int? minimumStaff,
        CancellationToken cancellationToken)
    {
        var minimum = minimumStaff ?? StaffingDefaults.MinimumStaffOnDuty;

        var teamMembers = await employees.ListByTeamAsync(teamId, cancellationToken);
        var teammatesExcludingRequester = teamMembers.Where(member => member.Id != request.EmployeeId).ToList();

        var teamApprovedLeave = await leaveRequests.ListByTeamAsync(
            teamId,
            RequestStatus.Approved,
            request.DateRange.Start,
            request.DateRange.End,
            cancellationToken);

        var holidays = await HolidaySupport.LoadAsync(
            holidayCalendars,
            HolidaySupport.YearsCoveredBy(request.DateRange.Start, request.DateRange.End),
            cancellationToken);

        var specification = new MinimumStaffingSpecification(minimum);
        var context = new TeamCoverageContext(teammatesExcludingRequester.Count, request.DateRange, teamApprovedLeave, holidays);

        return specification.IsSatisfiedBy(context)
            ? null
            : LeaveRequestErrors.MinimumStaffingNotMet(minimum, request.DateRange.Start);
    }
}
