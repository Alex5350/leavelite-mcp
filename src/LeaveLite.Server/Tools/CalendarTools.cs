using System.ComponentModel;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Balances;
using LeaveLite.Application.Calendars;
using LeaveLite.Application.Common;
using LeaveLite.Domain.Enums;
using LeaveLite.Infrastructure.Initialization;
using LeaveLite.Server.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace LeaveLite.Server.Tools;

/// <summary>Planning tools: team calendars, holidays and balance forecasts.</summary>
[McpServerToolType]
public static class CalendarTools
{
    [McpServerTool(Name = "get_team_calendar")]
    [Description(
        "Day-by-day calendar for a team over a date window: weekday, holidays, and which employees are on " +
        "APPROVED leave each day. Use for coverage questions like 'who is out the week of Sep 7?' or before " +
        "approving leave. Provide either a month (yyyy-MM) or an explicit from/to window (max 62 days); " +
        "with neither, the current month is shown. If teamName is omitted the single demo team is used.")]
    public static async Task<string> GetTeamCalendar(
        IServiceScopeFactory scopeFactory,
        [Description("Team name, e.g. 'Platform'. Optional — the demo organization has one team.")] string? teamName = null,
        [Description("Month to display as yyyy-MM, e.g. 2026-09. Mutually exclusive with from/to.")] string? month = null,
        [Description("First day of the window, ISO yyyy-MM-dd — use together with to.")] string? from = null,
        [Description("Last day of the window (inclusive), ISO yyyy-MM-dd — use together with from. Max 62 days.")] string? to = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        if (!DemoTeams.TryGetByName(teamName, out var team) && !string.IsNullOrWhiteSpace(teamName))
        {
            return $"Unknown team '{teamName}'. Known teams: {DemoTeams.Names()}.";
        }

        DateOnly windowStart;
        DateOnly windowEnd;

        if (!string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(to))
        {
            if (!string.IsNullOrWhiteSpace(month))
            {
                return "Provide either 'month' or 'from'/'to', not both.";
            }

            if (!ToolHelpers.TryParseDate(from, out windowStart))
            {
                return ToolHelpers.BadDate(nameof(from), from);
            }

            if (!ToolHelpers.TryParseDate(to, out windowEnd))
            {
                return ToolHelpers.BadDate(nameof(to), to);
            }
        }
        else if (!string.IsNullOrWhiteSpace(month))
        {
            if (!ToolHelpers.TryParseMonth(month, out windowStart))
            {
                return $"'month' must be in yyyy-MM format (got '{month}').";
            }

            windowEnd = windowStart.AddMonths(1).AddDays(-1);
        }
        else
        {
            var time = provider.GetRequiredService<IDateTimeProvider>();
            windowStart = new DateOnly(time.Today.Year, time.Today.Month, 1);
            windowEnd = windowStart.AddMonths(1).AddDays(-1);
        }

        var handler = provider.GetRequiredService<IQueryHandler<GetTeamCalendarQuery, IReadOnlyList<TeamCalendarEntry>>>();
        var result = await handler.Handle(new GetTeamCalendarQuery(team.Id, windowStart, windowEnd), cancellationToken);

        if (result.IsError)
        {
            return ToolHelpers.Failure(result.Errors);
        }

        var lines = result.Value.Select(entry =>
        {
            var date = entry.Date.ToString(ToolHelpers.DateFormat);
            var weekday = entry.Date.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture);

            if (entry.HolidayName is { } holiday)
            {
                return $"{date} {weekday,3}  HOLIDAY: {holiday}";
            }

            if (!entry.IsWorkingDay)
            {
                return $"{date} {weekday,3}  (weekend)";
            }

            return entry.EmployeesOnLeave.Count == 0
                ? $"{date} {weekday,3}  —"
                : $"{date} {weekday,3}  on leave: {string.Join(", ", entry.EmployeesOnLeave)}";
        });

        var workingDays = result.Value.Count(entry => entry.IsWorkingDay);
        var absentDays = result.Value.Count(entry => entry.EmployeesOnLeave.Count > 0);

        return $"Team calendar for '{team.Name}' {windowStart.ToString(ToolHelpers.DateFormat)} to {windowEnd.ToString(ToolHelpers.DateFormat)} " +
               $"({workingDays} working days, {absentDays} days with someone away):\n" + string.Join("\n", lines);
    }

    [McpServerTool(Name = "list_holidays")]
    [Description(
        "Lists the configured company holidays for a year (date + name) as text. Holidays are not charged as " +
        "leave days. Use to explain why a span has fewer working days than expected.")]
    public static async Task<string> ListHolidays(
        [Description("Calendar year to list holidays for, e.g. 2026")] int year,
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var handler = provider.GetRequiredService<IQueryHandler<ListHolidaysQuery, IReadOnlyList<HolidayDto>>>();
        var result = await handler.Handle(new ListHolidaysQuery(year), cancellationToken);

        if (result.IsError)
        {
            return ToolHelpers.Failure(result.Errors);
        }

        return result.Value.Count == 0
            ? $"No holidays are configured for {year}."
            : $"Holidays {year} ({result.Value.Count}):\n"
                + string.Join("\n", result.Value.Select(holiday => $"{holiday.Date.ToString(ToolHelpers.DateFormat)} {holiday.Name}"));
    }

    [McpServerTool(Name = "forecast_balance")]
    [Description(
        "Projects an employee's leave balance N months into the future (1-12): the current balance and the " +
        "projected balance at the horizon given continued accrual and already-approved leave. Use to answer " +
        "'can I afford a 2-week trip in December?'")]
    public static async Task<string> ForecastBalance(
        [Description("Work email of the employee, e.g. carla@leavelite.io (a Guid employee id also works)")] string employeeEmail,
        [Description("Leave type: Vacation, Sick or Parental — must match the employee's enrolled accrual policy")] string leaveType,
        [Description("How many months ahead to project, 1 to 12")] int monthsAhead,
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var employees = provider.GetRequiredService<IEmployeeRepository>();
        var employee = await ToolHelpers.FindEmployeeAsync(employees, employeeEmail, cancellationToken);
        if (employee is null)
        {
            return ToolHelpers.UnknownEmployee(employeeEmail);
        }

        if (!ToolHelpers.TryParseLeaveType(leaveType, out var parsedLeaveType))
        {
            return ToolHelpers.BadLeaveType(leaveType);
        }

        var handler = provider.GetRequiredService<IQueryHandler<ForecastBalanceQuery, ForecastBalanceDto>>();
        var result = await handler.Handle(
            new ForecastBalanceQuery(employee.Id, parsedLeaveType, monthsAhead),
            cancellationToken);

        return result.IsError
            ? ToolHelpers.Failure(result.Errors)
            : $"{employee.FullName} ({employee.Email.Value}) — {parsedLeaveType} balance forecast over {result.Value.MonthsAhead} month(s):\n" +
              $"- today: accrued {ToolHelpers.Hours(result.Value.Current.AccruedHours)}, consumed {ToolHelpers.Hours(result.Value.Current.ConsumedHours)}, " +
              $"available {ToolHelpers.Hours(result.Value.Current.BalanceHours)}\n" +
              $"- by {result.Value.Horizon.ToString(ToolHelpers.DateFormat)}: accrued {ToolHelpers.Hours(result.Value.Projected.AccruedHours)}, " +
              $"consumed {ToolHelpers.Hours(result.Value.Projected.ConsumedHours)}, available {ToolHelpers.Hours(result.Value.Projected.BalanceHours)}";
    }
}
