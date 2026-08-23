using ErrorOr;
using FluentValidation;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Common;
using LeaveLite.Domain.Common;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Errors;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Application.LeaveRequests;

/// <summary>One pending request enriched with employee name and computed leave hours.</summary>
public sealed record PendingApprovalItem(
    LeaveRequestId RequestId,
    string EmployeeName,
    LeaveType LeaveType,
    DateOnly Start,
    DateOnly End,
    int WorkingDays,
    decimal RequestedHours,
    DateTimeOffset SubmittedAtUtc,
    string? Reason);

/// <summary>All pending requests awaiting the manager's decision, for the manager's own team.</summary>
public sealed record ListPendingApprovalsQuery(EmployeeId ManagerId) : IQuery<IReadOnlyList<PendingApprovalItem>>;

public sealed class ListPendingApprovalsValidator : AbstractValidator<ListPendingApprovalsQuery>
{
    public ListPendingApprovalsValidator()
    {
        RuleFor(query => query.ManagerId).NotEqual(default(EmployeeId));
    }
}

internal sealed class ListPendingApprovalsHandler(
    IEmployeeRepository employees,
    ILeaveRequestRepository leaveRequests,
    IHolidayCalendarRepository holidayCalendars,
    IValidator<ListPendingApprovalsQuery> validator) : IQueryHandler<ListPendingApprovalsQuery, IReadOnlyList<PendingApprovalItem>>
{
    public async Task<ErrorOr<IReadOnlyList<PendingApprovalItem>>> Handle(ListPendingApprovalsQuery query, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorsAsync(query, cancellationToken) is { } validationErrors)
        {
            return validationErrors;
        }

        if (await employees.GetByIdAsync(query.ManagerId, cancellationToken) is not { } manager)
        {
            return EmployeeErrors.NotFound(query.ManagerId);
        }

        if (manager.TeamRole != TeamRole.Manager)
        {
            return EmployeeErrors.NotAManager(manager.Id);
        }

        var pending = await leaveRequests.ListByTeamAsync(manager.TeamId, RequestStatus.Pending, null, null, cancellationToken);
        if (pending.Count == 0)
        {
            return Array.Empty<PendingApprovalItem>();
        }

        var teamMembers = await employees.ListByTeamAsync(manager.TeamId, cancellationToken);
        var namesById = teamMembers.ToDictionary(static member => member.Id, static member => member.FullName);

        var holidays = await HolidaySupport.LoadAsync(
            holidayCalendars,
            HolidaySupport.CollectCoveredYears(pending, MinDate(pending), MaxDate(pending)),
            cancellationToken);

        var items = pending
            .Select(request => new PendingApprovalItem(
                request.Id,
                namesById.GetValueOrDefault(request.EmployeeId, $"<unknown:{request.EmployeeId}>"),
                request.LeaveType,
                request.DateRange.Start,
                request.DateRange.End,
                WorkSchedule.CountWorkingDays(request.DateRange, holidays),
                WorkSchedule.WorkingHours(request.DateRange, holidays),
                request.SubmittedAtUtc,
                request.Reason))
            .OrderBy(item => item.Start)
            .ToList();

        return items;
    }

    private static DateOnly MinDate(IReadOnlyList<Domain.LeaveRequests.LeaveRequest> requests)
        => requests.Min(static request => request.DateRange.Start);

    private static DateOnly MaxDate(IReadOnlyList<Domain.LeaveRequests.LeaveRequest> requests)
        => requests.Max(static request => request.DateRange.End);
}
