using System.ComponentModel;
using System.Globalization;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Calendars;
using LeaveLite.Application.Common;
using LeaveLite.Infrastructure.Initialization;
using LeaveLite.Server.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace LeaveLite.Server.Resources;

/// <summary>
/// Read-only MCP resources: configuration data an AI client can attach to a conversation as
/// context without calling a tool.
/// </summary>
[McpServerResourceType]
public static class LeaveLiteResources
{
    [McpServerResource(UriTemplate = "leavelite://policies", Name = "accrual-policies", MimeType = "text/plain")]
    [Description(
        "All leave accrual policies in force: leave type, eligible employment type, accrual period, hours per " +
        "period, annual/carry-over caps, minimum tenure and whether the balance is granted upfront. Read this to " +
        "understand why a balance looks the way it does.")]
    public static async Task<string> GetPolicies(
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        var policies = scope.ServiceProvider.GetRequiredService<IAccrualPolicyRepository>();
        var all = await policies.ListAsync(cancellationToken);

        var lines = all.Select(policy =>
            $"{policy.Name}: {policy.LeaveType} for {policy.EmploymentType}; " +
            $"{policy.HoursPerPeriod.ToString("0.##", CultureInfo.InvariantCulture)}h per {policy.AccrualPeriod.ToString().ToLowerInvariant()} " +
            $"(annual entitlement {policy.AnnualAmount.ToString("0.##", CultureInfo.InvariantCulture)}h)" +
            (policy.AnnualCapHours is { } annualCap ? $", annual cap {annualCap.ToString("0.##", CultureInfo.InvariantCulture)}h" : string.Empty) +
            (policy.CarryOverCapHours is { } carryOverCap ? $", carry-over cap {carryOverCap.ToString("0.##", CultureInfo.InvariantCulture)}h" : ", no carry-over cap") +
            $", minimum tenure {policy.MinTenureMonths} month(s)" +
            (policy.GrantsBalanceUpfront ? ", granted upfront once eligible" : ", accrued period by period"));

        return $"LeaveLite accrual policies ({all.Count}):\n" + string.Join("\n", lines);
    }

    [McpServerResource(UriTemplate = "leavelite://holidays/{year}", Name = "holidays", MimeType = "text/plain")]
    [Description(
        "The company holiday calendar for a year (e.g. leavelite://holidays/2026): each holiday's date and name. " +
        "Holidays are working-day exclusions — leave is never charged for them.")]
    public static async Task<string> GetHolidays(
        [Description("Four-digit year, e.g. 2026")] int year,
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        var handler = scope.ServiceProvider
            .GetRequiredService<IQueryHandler<ListHolidaysQuery, IReadOnlyList<HolidayDto>>>();

        var result = await handler.Handle(new ListHolidaysQuery(year), cancellationToken);

        return result.IsError
            ? ToolHelpers.Failure(result.Errors)
            : result.Value.Count == 0
                ? $"No holidays are configured for {year}."
                : $"Company holidays {year}:\n" + string.Join("\n",
                    result.Value.Select(holiday => $"{holiday.Date.ToString(ToolHelpers.DateFormat)} {holiday.Name}"));
    }

    [McpServerResource(UriTemplate = "leavelite://teams", Name = "teams", MimeType = "text/plain")]
    [Description("The team directory: team names and their stable ids.")]
    public static string GetTeams()
        => $"LeaveLite teams ({DemoTeams.All.Count}):\n"
            + string.Join("\n", DemoTeams.All.Select(team => $"{team.Name} (id {team.Id})"));
}
