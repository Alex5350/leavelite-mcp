using ErrorOr;
using FluentValidation;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Common;
using LeaveLite.Domain.Enums;

namespace LeaveLite.Application.Calendars;

/// <summary>One day of the team calendar.</summary>
public sealed record TeamCalendarEntry(
    DateOnly Date,
    DayOfWeek Weekday,
    string? HolidayName,
    IReadOnlyList<string> EmployeesOnLeave)
{
    /// <summary>Mon-Fri and not a holiday.</summary>
    public bool IsWorkingDay { get; init; }
}

/// <summary>
/// Per-day team view over a date range: who is on approved leave, holidays, weekday.
/// The range is capped at 62 days by the validator.
/// </summary>
public sealed record GetTeamCalendarQuery(Guid TeamId, DateOnly From, DateOnly To) : IQuery<IReadOnlyList<TeamCalendarEntry>>
{
    /// <summary>Convenience factory expanding any day of a month to the full month.</summary>
    public static GetTeamCalendarQuery ForMonth(Guid teamId, DateOnly month)
    {
        var start = new DateOnly(month.Year, month.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        return new GetTeamCalendarQuery(teamId, start, end);
    }
}

public sealed class GetTeamCalendarValidator : AbstractValidator<GetTeamCalendarQuery>
{
    public const int MaxRangeDays = 62;

    public GetTeamCalendarValidator()
    {
        RuleFor(query => query.TeamId).NotEqual(Guid.Empty);

        RuleFor(query => query.From).NotEmpty();
        RuleFor(query => query.To).NotEmpty();
        RuleFor(query => query.To).GreaterThanOrEqualTo(query => query.From);

        RuleFor(query => query)
            .Must(query => query.To.DayNumber - query.From.DayNumber + 1 <= MaxRangeDays)
            .WithMessage($"Calendar range is capped at {MaxRangeDays} days.");
    }
}

internal sealed class GetTeamCalendarHandler(
    IEmployeeRepository employees,
    ILeaveRequestRepository leaveRequests,
    IHolidayCalendarRepository holidayCalendars,
    IValidator<GetTeamCalendarQuery> validator) : IQueryHandler<GetTeamCalendarQuery, IReadOnlyList<TeamCalendarEntry>>
{
    public async Task<ErrorOr<IReadOnlyList<TeamCalendarEntry>>> Handle(GetTeamCalendarQuery query, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorsAsync(query, cancellationToken) is { } validationErrors)
        {
            return validationErrors;
        }

        var teamMembers = await employees.ListByTeamAsync(query.TeamId, cancellationToken);
        var namesById = teamMembers.ToDictionary(static member => member.Id, static member => member.FullName);

        var approvedLeave = await leaveRequests.ListByTeamAsync(query.TeamId, RequestStatus.Approved, query.From, query.To, cancellationToken);

        var holidays = await HolidaySupport.LoadAsync(
            holidayCalendars,
            HolidaySupport.YearsCoveredBy(query.From, query.To),
            cancellationToken);

        var entries = new List<TeamCalendarEntry>(query.To.DayNumber - query.From.DayNumber + 1);
        for (var date = query.From; date <= query.To; date = date.AddDays(1))
        {
            var holidayName = holidays is not null && holidays.TryGetHoliday(date, out var name) ? name : null;

            var onLeave = approvedLeave
                .Where(request => request.DateRange.Contains(date))
                .Select(request => namesById.GetValueOrDefault(request.EmployeeId, $"<unknown:{request.EmployeeId}>"))
                .Distinct()
                .ToList();

            entries.Add(new TeamCalendarEntry(
                date,
                date.DayOfWeek,
                holidayName,
                onLeave)
            {
                IsWorkingDay = Domain.Common.WorkSchedule.IsWorkingDay(date, holidays),
            });
        }

        return entries;
    }
}
