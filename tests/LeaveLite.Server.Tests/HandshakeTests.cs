using ModelContextProtocol.Client;

namespace LeaveLite.Server.Tests;

/// <summary>Protocol-level handshake: the server must advertise its tools, resources and prompts.</summary>
public sealed class HandshakeTests : IClassFixture<McpServerFactory>
{
    /// <summary>The complete LeaveLite tool surface (LeaveTools 3, ApprovalTools 2, CalendarTools 3, DirectoryTools 2).</summary>
    public static readonly string[] AllToolNames =
    [
        "check_employee_balance",
        "request_leave",
        "cancel_leave_request",
        "list_pending_approvals",
        "decide_leave_request",
        "get_team_calendar",
        "list_holidays",
        "forecast_balance",
        "list_employees",
        "who_reports_to_manager",
    ];

    private readonly McpServerFactory _factory;

    public HandshakeTests(McpServerFactory factory) => _factory = factory;

    [Fact]
    public async Task ListTools_ReturnsEveryLeavLiteTool()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var names = tools.Select(tool => tool.Name).ToList();

        foreach (var expected in AllToolNames)
        {
            Assert.True(names.Contains(expected), $"Expected tool '{expected}' but the server listed: {string.Join(", ", names.Order())}");
        }
    }

    [Fact]
    public async Task ListTools_ToolDescriptionsArePresent()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.All(tools, tool => Assert.False(string.IsNullOrWhiteSpace(tool.Description)));
    }

    [Fact]
    public async Task ListResources_IncludesPoliciesAndTeams()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var resources = await client.ListResourcesAsync(cancellationToken: TestContext.Current.CancellationToken);
        var uris = resources.Select(resource => resource.Uri).ToList();

        Assert.Contains("leavelite://policies", uris);
        Assert.Contains("leavelite://teams", uris);
    }

    [Fact]
    public async Task ListResourceTemplates_IncludesTheHolidaysByYearTemplate()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var templates = await client.ListResourceTemplatesAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(templates, template => template.UriTemplate == "leavelite://holidays/{year}");
    }

    [Fact]
    public async Task ListPrompts_IncludesTheTeamCoverageReviewPrompt()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var prompts = await client.ListPromptsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(prompts, prompt => prompt.Name == "team-coverage-review");
    }
}
