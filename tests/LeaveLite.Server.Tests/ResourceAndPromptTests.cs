using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LeaveLite.Server.Tests;

/// <summary>MCP resources and prompts served by the LeaveLite host.</summary>
public sealed class ResourceAndPromptTests : IClassFixture<McpServerFactory>
{
    private readonly McpServerFactory _factory;

    public ResourceAndPromptTests(McpServerFactory factory) => _factory = factory;

    [Fact]
    public async Task PoliciesResource_ListsTheVacationPolicy()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await client.ReadResourceAsync("leavelite://policies", cancellationToken: TestContext.Current.CancellationToken);
        var text = result.Text();

        Assert.Contains("Vacation", text);
        Assert.Contains("16h per monthly", text);
        Assert.Contains("minimum tenure 3 month(s)", text);
    }

    [Fact]
    public async Task TeamsResource_ListsThePlatformTeam()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await client.ReadResourceAsync("leavelite://teams", cancellationToken: TestContext.Current.CancellationToken);
        var text = result.Text();

        Assert.Contains("Platform", text);
        Assert.Contains("e700e69a-9011-5c51-aa72-781b86f26323", text); // documented stable team id
    }

    [Fact]
    public async Task HolidaysResourceTemplate_For2026_ListsKnownHolidays()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await client.ReadResourceAsync(
            "leavelite://holidays/{year}",
            new Dictionary<string, object?> { ["year"] = "2026" },
            cancellationToken: TestContext.Current.CancellationToken);
        var text = result.Text();

        Assert.Contains("Company holidays 2026", text);
        Assert.Contains("New Year's Day", text);
        Assert.Contains("Labor Day", text);
    }

    [Fact]
    public async Task TeamCoverageReviewPrompt_ReturnsNonEmptyUserMessage()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await client.GetPromptAsync(
            "team-coverage-review",
            new Dictionary<string, object?> { ["teamName"] = "Platform", ["month"] = "2026-09" },
            cancellationToken: TestContext.Current.CancellationToken);

        var message = Assert.Single(result.Messages);
        var text = Assert.IsType<TextContentBlock>(message.Content).Text;

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Contains("Platform", text);
        Assert.Contains("2026-09", text);
        Assert.Contains("get_team_calendar", text);
        Assert.Contains("list_pending_approvals", text);
    }
}
