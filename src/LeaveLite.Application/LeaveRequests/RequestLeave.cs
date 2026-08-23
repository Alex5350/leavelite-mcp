using ErrorOr;
using FluentValidation;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Common;
using LeaveLite.Domain.Balances;
using LeaveLite.Domain.Common;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Errors;
using LeaveLite.Domain.LeaveRequests;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Application.LeaveRequests;

/// <summary>
/// Submits a leave request. Validated here (not in the aggregate): start is today or later,
/// no overlap with the employee's own Pending/Approved requests, and the balance must cover the
/// new request after deduction. Minimum staffing is checked at approval, not submission.
/// </summary>
public sealed record RequestLeaveCommand(
    EmployeeId EmployeeId,
    LeaveType LeaveType,
    DateOnly Start,
    DateOnly End,
    string? Reason = null) : ICommand<LeaveRequestId>;

public sealed class RequestLeaveValidator : AbstractValidator<RequestLeaveCommand>
{
    public RequestLeaveValidator()
    {
        RuleFor(command => command.EmployeeId).NotEqual(default(EmployeeId));
        RuleFor(command => command.LeaveType).IsInEnum();

        RuleFor(command => command.Start).NotEmpty();

        RuleFor(command => command.End)
            .NotEmpty()
            .GreaterThanOrEqualTo(command => command.Start)
            .WithMessage("Leave end must be on or after the start date.");

        RuleFor(command => command.Reason).MaximumLength(2000);
    }
}

internal sealed class RequestLeaveHandler(
    IEmployeeRepository employees,
    ILeaveRequestRepository leaveRequests,
    IAccrualPolicyRepository policies,
    IHolidayCalendarRepository holidayCalendars,
    IBalanceCalculator calculator,
    IDateTimeProvider time,
    IUnitOfWork unitOfWork,
    IDomainEventDispatcher dispatcher,
    IValidator<RequestLeaveCommand> validator) : ICommandHandler<RequestLeaveCommand, LeaveRequestId>
{
    public async Task<ErrorOr<LeaveRequestId>> Handle(RequestLeaveCommand command, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorsAsync(command, cancellationToken) is { } validationErrors)
        {
            return validationErrors;
        }

        // Future-only rule (clock-driven business rule): start must be today or later.
        if (command.Start < time.Today)
        {
            return LeaveRequestErrors.StartDateInPast(command.Start, time.Today);
        }

        if (await employees.GetByIdAsync(command.EmployeeId, cancellationToken) is not { } employee)
        {
            return EmployeeErrors.NotFound(command.EmployeeId);
        }

        if (await policies.GetByIdAsync(employee.AccrualPolicyId, cancellationToken) is not { } policy)
        {
            return AccrualPolicyErrors.NotFound(employee.AccrualPolicyId.Value);
        }

        if (policy.LeaveType != command.LeaveType)
        {
            return EmployeeErrors.NoPolicyForLeaveType(command.LeaveType);
        }

        var rangeResult = DateRange.Create(command.Start, command.End);
        if (rangeResult.IsError)
        {
            return rangeResult.Errors;
        }

        var range = rangeResult.Value;

        // Overlap guard: the employee's own pending/approved requests must not collide with the new range.
        var overlapping = await leaveRequests.GetOverlappingAsync(employee.Id, range, cancellationToken);
        if (overlapping.Any(request => request.Status is RequestStatus.Pending or RequestStatus.Approved))
        {
            return LeaveRequestErrors.OverlappingRequest(range.Start, range.End);
        }

        // Balance guard: current balance minus this request's working-day hours must stay >= 0.
        var history = await leaveRequests.ListByEmployeeAsync(employee.Id, cancellationToken);
        var holidays = await HolidaySupport.LoadAsync(
            holidayCalendars,
            HolidaySupport.CollectCoveredYears(history, time.Today, range.End),
            cancellationToken);

        var balance = calculator.Calculate(employee, policy, time.Today, history, holidays);
        var requestedHours = WorkSchedule.WorkingHours(range, holidays);
        var remainingHours = balance.BalanceHours - requestedHours;
        if (remainingHours < 0)
        {
            return LeaveRequestErrors.InsufficientBalance(requestedHours, balance.BalanceHours, -remainingHours);
        }

        var created = LeaveRequest.Create(employee.Id, command.LeaveType, range, command.Reason, time.UtcNow);
        if (created.IsError)
        {
            return created.Errors;
        }

        var request = created.Value;

        await leaveRequests.AddAsync(request, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (remainingHours < StaffingDefaults.LowBalanceThresholdHours)
        {
            await dispatcher.DispatchAsync(
                new LowBalanceWarningDomainEvent(employee.Id, command.LeaveType, remainingHours, StaffingDefaults.LowBalanceThresholdHours, time.Today),
                cancellationToken);
        }

        return request.Id;
    }
}
