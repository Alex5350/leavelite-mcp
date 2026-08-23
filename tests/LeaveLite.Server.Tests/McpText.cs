using System.Globalization;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LeaveLite.Server.Tests;

/// <summary>Helpers for driving the LeaveLite tools through an MCP client.</summary>
internal static partial class McpText
{
    /// <summary>The marker every LeaveLite tool failure text starts with.</summary>
    public const string FailureMarker = "Request failed";

    /// <summary>Calls a tool and returns its concatenated text content blocks.</summary>
    public static async Task<string> ToolTextAsync(
        this McpClient client,
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments = null)
    {
        var result = await client.CallToolAsync(toolName, arguments, cancellationToken: TestContext.Current.CancellationToken);
        return string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));
    }

    /// <summary>Concatenates the text blocks of a resource read result.</summary>
    public static string Text(this ReadResourceResult result)
        => string.Concat(result.Contents.OfType<TextResourceContents>().Select(contents => contents.Text));

    /// <summary>Parses "available 466.84h" out of a check_employee_balance response.</summary>
    public static decimal AvailableHours(string balanceText)
    {
        var match = AvailablePattern().Match(balanceText);
        Assert.True(match.Success, $"Expected an 'available ...h' figure in: {balanceText}");
        return decimal.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>Extracts the first Guid from text like "Request id: 0d1e...".</summary>
    public static Guid FirstGuid(string text)
    {
        var match = GuidPattern().Match(text);
        Assert.True(match.Success, $"Expected a Guid in: {text}");
        return Guid.Parse(match.Value);
    }

    /// <summary>Extracts the request id Guid of the pending-queue line naming the given employee.</summary>
    public static Guid RequestIdFor(string pendingApprovalsText, string employeeName)
    {
        var line = pendingApprovalsText.Split('\n').SingleOrDefault(candidate => candidate.Contains(employeeName, StringComparison.OrdinalIgnoreCase));
        Assert.False(line is null, $"No pending queue line for '{employeeName}' in: {pendingApprovalsText}");
        return FirstGuid(line!);
    }

    /// <summary>
    /// A future Mon-Fri week guaranteed free of every seeded leave request. The seed only ever
    /// books dates within the next ~2 months, so anchoring at the first Monday three months out
    /// (then shifting by <paramref name="weekOffset"/> whole weeks) is always safe, and tests
    /// sharing a class fixture get mutually disjoint weeks.
    /// </summary>
    public static (DateOnly Monday, DateOnly Friday) FreeFutureWeek(int weekOffset = 0)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var anchor = new DateOnly(today.Year, today.Month, 1).AddMonths(3);
        while (anchor.DayOfWeek != DayOfWeek.Monday)
        {
            anchor = anchor.AddDays(1);
        }

        var monday = anchor.AddDays(7 * weekOffset);
        return (monday, monday.AddDays(4));
    }

    [GeneratedRegex(@"available (\d+(?:\.\d+)?)h")]
    private static partial Regex AvailablePattern();

    [GeneratedRegex(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase)]
    private static partial Regex GuidPattern();
}
