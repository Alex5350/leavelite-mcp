using ModelContextProtocol.Client;

namespace LeaveLite.Server.Tests;

/// <summary>Read-only tool surface over the seeded demo organization.</summary>
public sealed class ToolReadTests : IClassFixture<McpServerFactory>
{
    private readonly McpServerFactory _factory;

    public ToolReadTests(McpServerFactory factory) => _factory = factory;

    [Fact]
    public async Task ListEmployees_ShowsTheDemoDirectoryWithRoles()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var text = await client.ToolTextAsync("list_employees");

        Assert.DoesNotContain(McpText.FailureMarker, text);
        foreach (var email in new[] { "ada@leavelite.io", "bruno@leavelite.io", "carla@leavelite.io", "dana@leavelite.io", "erin@leavelite.io" })
        {
            Assert.Contains(email, text);
        }
        Assert.Contains("Ada Lovelace", text);
        Assert.Contains("Manager", text); // ada manages the Platform team
        Assert.Contains("Platform", text);
    }

    [Fact]
    public async Task CheckBalance_ForAdaVacation_IsPositiveWithoutErrorMarker()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var text = await client.ToolTextAsync(
            "check_employee_balance",
            new Dictionary<string, object?> { ["employeeEmail"] = "ada@leavelite.io", ["leaveType"] = "Vacation" });

        Assert.DoesNotContain(McpText.FailureMarker, text);
        Assert.Contains("Vacation", text);
        Assert.True(McpText.AvailableHours(text) > 0m, $"Expected a positive balance in: {text}");
    }

    [Fact]
    public async Task CheckBalance_WithoutLeaveType_UsesTheEnrolledPolicy()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var text = await client.ToolTextAsync(
            "check_employee_balance",
            new Dictionary<string, object?> { ["employeeEmail"] = "dana@leavelite.io" });

        Assert.DoesNotContain(McpText.FailureMarker, text);
        Assert.Contains("Sick", text); // dana is enrolled in the upfront Sick policy
    }

    [Fact]
    public async Task CheckBalance_ForLeaveTypeWithoutEnrolledPolicy_SurfacesTheErrorCode()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var text = await client.ToolTextAsync(
            "check_employee_balance",
            new Dictionary<string, object?> { ["employeeEmail"] = "bruno@leavelite.io", ["leaveType"] = "Parental" });

        Assert.Contains("[Employee.NoPolicyForLeaveType]", text);
    }

    [Fact]
    public async Task CheckBalance_UnknownEmployee_PointsAtTheDirectory()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var text = await client.ToolTextAsync(
            "check_employee_balance",
            new Dictionary<string, object?> { ["employeeEmail"] = "nobody@leavelite.io", ["leaveType"] = "Vacation" });

        Assert.Contains("No employee matches 'nobody@leavelite.io'", text);
        Assert.Contains("list_employees", text);
    }

    [Fact]
    public async Task ListPendingApprovals_ForAda_ShowsTheSeededPendingQueue()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var text = await client.ToolTextAsync(
            "list_pending_approvals",
            new Dictionary<string, object?> { ["managerEmail"] = "ada@leavelite.io" });

        Assert.DoesNotContain(McpText.FailureMarker, text);
        Assert.Contains("Carla Gomez", text); // seeded pending request next week
        Assert.Contains("Erin Davis", text); // seeded pending request the week after
    }

    [Fact]
    public async Task ListHolidays_For2026_ListsTheSeededHolidayCalendar()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var text = await client.ToolTextAsync("list_holidays", new Dictionary<string, object?> { ["year"] = 2026 });

        Assert.DoesNotContain(McpText.FailureMarker, text);
        Assert.Contains("2026-01-01", text);
        Assert.Contains("New Year's Day", text);
        Assert.Contains("Christmas Day", text);
    }

    [Fact]
    public async Task GetTeamCalendar_ForSeptember2026_MarksHolidaysAndWeekends()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var text = await client.ToolTextAsync(
            "get_team_calendar",
            new Dictionary<string, object?> { ["teamName"] = "Platform", ["month"] = "2026-09" });

        Assert.DoesNotContain(McpText.FailureMarker, text);
        Assert.Contains("Team calendar for 'Platform' 2026-09-01 to 2026-09-30", text);
        Assert.Contains("HOLIDAY: Labor Day", text); // 2026-09-07 is a seeded holiday
        Assert.Contains("(weekend)", text);
        Assert.Contains("2026-09-07", text);
    }

    [Fact]
    public async Task ForecastBalance_ForBruno_ProjectsGrowth()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var text = await client.ToolTextAsync(
            "forecast_balance",
            new Dictionary<string, object?> { ["employeeEmail"] = "bruno@leavelite.io", ["leaveType"] = "Vacation", ["monthsAhead"] = 3 });

        Assert.DoesNotContain(McpText.FailureMarker, text);
        Assert.Contains("today:", text);
        Assert.Contains("by ", text);
    }

    [Fact]
    public async Task WhoReportsToManager_ForAda_ListsHerTeam()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var text = await client.ToolTextAsync(
            "who_reports_to_manager",
            new Dictionary<string, object?> { ["managerEmail"] = "ada@leavelite.io" });

        Assert.DoesNotContain(McpText.FailureMarker, text);
        Assert.Contains("Bruno Chen", text);
        Assert.DoesNotContain("ada@leavelite.io —", text); // report lines are email-first; the manager is not her own report
    }
}
