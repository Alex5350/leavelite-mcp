using System.ComponentModel;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Common;
using LeaveLite.Application.LeaveRequests;
using LeaveLite.Domain.ValueObjects;
using LeaveLite.Server.Tools;
using ModelContextProtocol.Server;

namespace LeaveLite.Server.Tools;

/// <summary>Manager-facing approval tools: the pending queue and decisions on it.</summary>
[McpServerToolType]
public static class ApprovalTools
{
    [McpServerTool(Name = "list_pending_approvals")]
    [Description(
        "Lists the leave requests Pending a manager's decision in their team, with request id, employee, leave " +
        "type, dates, working days, requested hours, submission time and reason. A team manager's to-do list — " +
        "call decide_leave_request with the returned request id to act on each item.")]
    public static async Task<string> ListPendingApprovals(
        [Description("Work email of the manager whose pending queue to list, e.g. ada@leavelite.io (a Guid employee id also works)")] string managerEmail,
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var employees = provider.GetRequiredService<IEmployeeRepository>();
        var manager = await ToolHelpers.FindEmployeeAsync(employees, managerEmail, cancellationToken);
        if (manager is null)
        {
            return ToolHelpers.UnknownEmployee(managerEmail);
        }

        var handler = provider.GetRequiredService<IQueryHandler<ListPendingApprovalsQuery, IReadOnlyList<PendingApprovalItem>>>();
        var result = await handler.Handle(new ListPendingApprovalsQuery(manager.Id), cancellationToken);

        if (result.IsError)
        {
            return ToolHelpers.Failure(result.Errors);
        }

        if (result.Value.Count == 0)
        {
            return $"No pending leave requests for {manager.FullName}'s team.";
        }

        var lines = result.Value.Select(item =>
            $"- {item.RequestId}: {item.EmployeeName}, {item.LeaveType} {item.Start.ToString(ToolHelpers.DateFormat)} to {item.End.ToString(ToolHelpers.DateFormat)} " +
            $"({item.WorkingDays} working days = {ToolHelpers.Hours(item.RequestedHours)}), submitted {item.SubmittedAtUtc:yyyy-MM-dd HH:mm} UTC" +
            (string.IsNullOrWhiteSpace(item.Reason) ? string.Empty : $", reason: \"{item.Reason}\""));

        return $"{result.Value.Count} pending leave request(s) for {manager.FullName}'s team:\n" + string.Join("\n", lines);
    }

    [McpServerTool(Name = "decide_leave_request")]
    [Description(
        "Approves or denies a pending leave request. Only a Manager of the same team as the requesting employee " +
        "may decide. Approving enforces minimum staffing (at least one other team member available on every " +
        "working day of the leave). Denying requires a denialReason.")]
    public static async Task<string> DecideLeaveRequest(
        [Description("Guid id of the leave request to decide, as returned by list_pending_approvals")] string requestId,
        [Description("Work email of the deciding manager, e.g. ada@leavelite.io (a Guid employee id also works)")] string approverEmail,
        [Description("true to approve the request, false to deny it")] bool approve,
        IServiceScopeFactory scopeFactory,
        [Description("Reason for denial — required when approve is false, ignored otherwise")] string? denialReason = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var employees = provider.GetRequiredService<IEmployeeRepository>();
        var approver = await ToolHelpers.FindEmployeeAsync(employees, approverEmail, cancellationToken);
        if (approver is null)
        {
            return ToolHelpers.UnknownEmployee(approverEmail);
        }

        if (!ToolHelpers.TryParseRequestId(requestId, out var parsedRequestId))
        {
            return $"'requestId' must be a Guid (got '{requestId}'). Find ids via list_pending_approvals.";
        }

        var handler = provider.GetRequiredService<ICommandHandler<DecideLeaveRequestCommand>>();
        var result = await handler.Handle(
            new DecideLeaveRequestCommand(parsedRequestId, approver.Id, approve, denialReason),
            cancellationToken);

        if (result.IsError)
        {
            return ToolHelpers.Failure(result.Errors);
        }

        return approve
            ? $"Leave request {parsedRequestId} APPROVED by {approver.FullName} ({approver.Email.Value}). The employee's balance will be charged for its working days."
            : $"Leave request {parsedRequestId} DENIED by {approver.FullName} ({approver.Email.Value}). Denial reason: {denialReason}";
    }
}
