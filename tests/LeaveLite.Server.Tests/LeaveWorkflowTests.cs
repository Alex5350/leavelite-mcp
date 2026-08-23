using System.Globalization;
using ModelContextProtocol.Client;

namespace LeaveLite.Server.Tests;

/// <summary>
/// The full write flow and error surfacing, all through the MCP protocol. Every test is
/// self-contained: it submits its own leave requests against the seeded demo database.
/// </summary>
public sealed class LeaveWorkflowTests : IClassFixture<McpServerFactory>
{
    private static Dictionary<string, object?> BrunoVacation(DateOnly start, DateOnly end, string? reason = null)
        => new()
        {
            ["employeeEmail"] = "bruno@leavelite.io",
            ["leaveType"] = "Vacation",
            ["startDate"] = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["endDate"] = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["reason"] = reason,
        };

    private readonly McpServerFactory _factory;

    public LeaveWorkflowTests(McpServerFactory factory) => _factory = factory;

    [Fact]
    public async Task Request_Approve_Flow_DeductsBalanceAtTheEnd()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var balanceBefore = McpText.AvailableHours(await client.ToolTextAsync(
            "check_employee_balance",
            new Dictionary<string, object?> { ["employeeEmail"] = "bruno@leavelite.io", ["leaveType"] = "Vacation" }));

        // 1. Submit a future vacation week for bruno.
        var (monday, friday) = McpText.FreeFutureWeek(0);
        var submission = await client.ToolTextAsync("request_leave", BrunoVacation(monday, friday, "Protocol test trip"));
        Assert.DoesNotContain(McpText.FailureMarker, submission);
        Assert.Contains("Status: Pending", submission);
        var requestId = McpText.FirstGuid(submission.Split("Request id:")[1]);

        // 2. The request lands in ada's pending queue.
        var queue = await client.ToolTextAsync(
            "list_pending_approvals",
            new Dictionary<string, object?> { ["managerEmail"] = "ada@leavelite.io" });
        Assert.DoesNotContain(McpText.FailureMarker, queue);
        Assert.Contains(requestId.ToString(), queue);
        Assert.Contains("Bruno Chen", queue);

        // 3. Ada approves it.
        var decision = await client.ToolTextAsync(
            "decide_leave_request",
            new Dictionary<string, object?> { ["requestId"] = requestId.ToString(), ["approverEmail"] = "ada@leavelite.io", ["approve"] = true });
        Assert.DoesNotContain(McpText.FailureMarker, decision);
        Assert.Contains("APPROVED", decision);

        // 4. Bruno's available balance is now strictly lower than before the request.
        var balanceAfter = McpText.AvailableHours(await client.ToolTextAsync(
            "check_employee_balance",
            new Dictionary<string, object?> { ["employeeEmail"] = "bruno@leavelite.io", ["leaveType"] = "Vacation" }));
        Assert.True(balanceAfter < balanceBefore, $"Expected balance to drop from {balanceBefore}h to {balanceAfter}h after approving a week of leave.");

        // 5. The approved request is gone from the pending queue.
        var queueAfter = await client.ToolTextAsync(
            "list_pending_approvals",
            new Dictionary<string, object?> { ["managerEmail"] = "ada@leavelite.io" });
        Assert.DoesNotContain(requestId.ToString(), queueAfter);
    }

    [Fact]
    public async Task Request_ThenCancelByOwner_Succeeds()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var (monday, friday) = McpText.FreeFutureWeek(1);
        var submission = await client.ToolTextAsync("request_leave", BrunoVacation(monday, friday));
        var requestId = McpText.FirstGuid(submission.Split("Request id:")[1]);

        var cancellation = await client.ToolTextAsync(
            "cancel_leave_request",
            new Dictionary<string, object?> { ["requestId"] = requestId.ToString(), ["employeeEmail"] = "bruno@leavelite.io" });

        Assert.DoesNotContain(McpText.FailureMarker, cancellation);
        Assert.Contains("cancelled", cancellation);
    }

    [Fact]
    public async Task OverlappingSecondRequest_IsRejectedWithTheErrorCode()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var (monday, friday) = McpText.FreeFutureWeek(2);
        var first = await client.ToolTextAsync("request_leave", BrunoVacation(monday, friday));
        Assert.DoesNotContain(McpText.FailureMarker, first);

        var second = await client.ToolTextAsync("request_leave", BrunoVacation(monday.AddDays(2), friday.AddDays(2)));

        Assert.Contains("[LeaveRequest.OverlappingRequest]", second);
    }

    [Fact]
    public async Task EndBeforeStart_IsRejectedByTheValidator()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var text = await client.ToolTextAsync("request_leave", BrunoVacation(new DateOnly(2026, 9, 18), new DateOnly(2026, 9, 14)));

        // The FluentValidation rule fires before DateRange.Create, so the surfaced code is the
        // property name "End" — not DateRange.StartAfterEnd.
        Assert.Contains("[End]", text);
        Assert.Contains("on or after the start date", text);
    }

    [Fact]
    public async Task DecideByANonManager_IsForbidden()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var queue = await client.ToolTextAsync(
            "list_pending_approvals",
            new Dictionary<string, object?> { ["managerEmail"] = "ada@leavelite.io" });
        var carlaRequestId = McpText.RequestIdFor(queue, "Carla Gomez");

        var decision = await client.ToolTextAsync(
            "decide_leave_request",
            new Dictionary<string, object?> { ["requestId"] = carlaRequestId.ToString(), ["approverEmail"] = "bruno@leavelite.io", ["approve"] = true });

        Assert.Contains("[LeaveRequest.ApproverNotTeamManager]", decision);
    }

    [Fact]
    public async Task DecideAnAlreadyDecidedRequest_Conflicts()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var (monday, friday) = McpText.FreeFutureWeek(3);
        var submission = await client.ToolTextAsync("request_leave", BrunoVacation(monday, friday));
        var requestId = McpText.FirstGuid(submission.Split("Request id:")[1]);

        var firstDecision = await client.ToolTextAsync(
            "decide_leave_request",
            new Dictionary<string, object?> { ["requestId"] = requestId.ToString(), ["approverEmail"] = "ada@leavelite.io", ["approve"] = true });
        Assert.DoesNotContain(McpText.FailureMarker, firstDecision);

        var secondDecision = await client.ToolTextAsync(
            "decide_leave_request",
            new Dictionary<string, object?> { ["requestId"] = requestId.ToString(), ["approverEmail"] = "ada@leavelite.io", ["approve"] = false, ["denialReason"] = "Changed my mind" });

        Assert.Contains("[LeaveRequest.AlreadyDecided]", secondDecision);
    }

    [Fact]
    public async Task DenyWithoutReason_IsRejected()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var queue = await client.ToolTextAsync(
            "list_pending_approvals",
            new Dictionary<string, object?> { ["managerEmail"] = "ada@leavelite.io" });
        var carlaRequestId = McpText.RequestIdFor(queue, "Carla Gomez");

        var denial = await client.ToolTextAsync(
            "decide_leave_request",
            new Dictionary<string, object?> { ["requestId"] = carlaRequestId.ToString(), ["approverEmail"] = "ada@leavelite.io", ["approve"] = false });

        Assert.Contains("[DenialReason]", denial);
    }

    [Fact]
    public async Task CancelBySomeoneElse_IsForbidden()
    {
        await using var client = await _factory.ConnectAsync(TestContext.Current.CancellationToken);

        var queue = await client.ToolTextAsync(
            "list_pending_approvals",
            new Dictionary<string, object?> { ["managerEmail"] = "ada@leavelite.io" });
        var carlaRequestId = McpText.RequestIdFor(queue, "Carla Gomez");

        var cancellation = await client.ToolTextAsync(
            "cancel_leave_request",
            new Dictionary<string, object?> { ["requestId"] = carlaRequestId.ToString(), ["employeeEmail"] = "bruno@leavelite.io" });

        Assert.Contains("[LeaveRequest.NotOwner]", cancellation);
    }
}
