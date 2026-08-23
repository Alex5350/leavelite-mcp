using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace LeaveLite.Server.Prompts;

/// <summary>Ready-made prompt templates for recurring AI-assisted workflows.</summary>
[McpServerPromptType]
public static class LeaveLitePrompts
{
    [McpServerPrompt(Name = "team-coverage-review")]
    [Description(
        "Builds a review prompt for a manager: check who is on leave, whether pending requests can be approved " +
        "safely, and who stays available — using the team calendar and pending approvals tools.")]
    public static ChatMessage TeamCoverageReview(
        [Description("Team name to review, e.g. 'Platform'")] string teamName,
        [Description("Month to review as yyyy-MM, e.g. 2026-09")] string month)
        => new(
            ChatRole.User,
            $"""
            You are reviewing leave coverage for the '{teamName}' team during {month}. Work through the following steps and finish with a short summary:

            1. Call get_team_calendar with teamName '{teamName}' and month '{month}' to see day-by-day availability, holidays and who is already on approved leave.
            2. Call list_pending_approvals for the team's manager (find the manager via list_employees if needed) to see requests awaiting a decision.
            3. For each pending request, judge the coverage impact: does approving leave any working day with fewer than two team members available? Highlight any thin-coverage days.
            4. Optionally call forecast_balance for the requesting employees to confirm their balances support the requested days.

            Conclude with: (a) a recommendation per pending request (approve / deny with reason), (b) the riskiest coverage days of the month, and (c) any holiday collisions worth knowing about.
            """);
}
