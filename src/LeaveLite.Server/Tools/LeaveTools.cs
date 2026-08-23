using System.ComponentModel;
using ErrorOr;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Balances;
using LeaveLite.Application.Common;
using LeaveLite.Application.Employees;
using LeaveLite.Application.LeaveRequests;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.ValueObjects;
using LeaveLite.Server.Tools;
using ModelContextProtocol.Server;

namespace LeaveLite.Server.Tools;

/// <summary>Employee-facing leave tools: balance checks, submitting and cancelling requests.</summary>
[McpServerToolType]
public static class LeaveTools
{
    [McpServerTool(Name = "check_employee_balance")]
    [Description(
        "Checks an employee's leave balance: hours accrued so far, hours consumed by approved leave, and the " +
        "hours still available. Call this before request_leave to see whether a vacation fits, or whenever someone " +
        "asks 'how much leave do I have left'. If leaveType is omitted, the leave type of the employee's enrolled " +
        "accrual policy is used.")]
    public static async Task<string> CheckEmployeeBalance(
        [Description("Work email of the employee, e.g. ada@leavelite.io (a Guid employee id also works)")] string employeeEmail,
        IServiceScopeFactory scopeFactory,
        [Description("Leave type to check: Vacation, Sick or Parental. Optional — defaults to the employee's enrolled policy.")] string? leaveType = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var employees = provider.GetRequiredService<IEmployeeRepository>();
        var policies = provider.GetRequiredService<IAccrualPolicyRepository>();

        var employee = await ToolHelpers.FindEmployeeAsync(employees, employeeEmail, cancellationToken);
        if (employee is null)
        {
            return ToolHelpers.UnknownEmployee(employeeEmail);
        }

        LeaveType effectiveLeaveType;
        if (string.IsNullOrWhiteSpace(leaveType))
        {
            var policy = await policies.GetByIdAsync(employee.AccrualPolicyId, cancellationToken);
            if (policy is null)
            {
                return ToolHelpers.Failure([Error.NotFound("AccrualPolicy.NotFound", $"Employee's accrual policy '{employee.AccrualPolicyId}' was not found.")]);
            }

            effectiveLeaveType = policy.LeaveType;
        }
        else if (!ToolHelpers.TryParseLeaveType(leaveType, out effectiveLeaveType))
        {
            return ToolHelpers.BadLeaveType(leaveType);
        }

        var handler = provider.GetRequiredService<IQueryHandler<CheckBalanceQuery, BalanceDto>>();
        var result = await handler.Handle(new CheckBalanceQuery(employee.Id, effectiveLeaveType), cancellationToken);

        return result.IsError
            ? ToolHelpers.Failure(result.Errors)
            : $"{employee.FullName} ({employee.Email.Value}) — {result.Value.LeaveType} balance as of {result.Value.AsOf.ToString(ToolHelpers.DateFormat)}: " +
              $"accrued {ToolHelpers.Hours(result.Value.AccruedHours)}, consumed {ToolHelpers.Hours(result.Value.ConsumedHours)}, " +
              $"available {ToolHelpers.Hours(result.Value.BalanceHours)}.";
    }

    [McpServerTool(Name = "request_leave")]
    [Description(
        "Submits a new leave request on behalf of an employee. The start date must be today or later, must not " +
        "overlap the employee's own pending/approved requests, and their balance must cover the working days " +
        "requested (weekends and holidays are never charged). The request starts in Pending status and needs a " +
        "team manager's approval via decide_leave_request.")]
    public static async Task<string> RequestLeave(
        [Description("Work email of the employee requesting leave, e.g. bruno@leavelite.io (a Guid employee id also works)")] string employeeEmail,
        [Description("Leave type: Vacation, Sick or Parental — must match the employee's enrolled accrual policy")] string leaveType,
        [Description("First day of leave, ISO date format yyyy-MM-dd")] string startDate,
        [Description("Last day of leave (inclusive), ISO date format yyyy-MM-dd, same as or after startDate")] string endDate,
        IServiceScopeFactory scopeFactory,
        [Description("Optional free-text reason for the request, shown to the approving manager")] string? reason = null,
        CancellationToken cancellationToken = default)
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

        if (!ToolHelpers.TryParseDate(startDate, out var start))
        {
            return ToolHelpers.BadDate(nameof(startDate), startDate);
        }

        if (!ToolHelpers.TryParseDate(endDate, out var end))
        {
            return ToolHelpers.BadDate(nameof(endDate), endDate);
        }

        var handler = provider.GetRequiredService<ICommandHandler<RequestLeaveCommand, LeaveRequestId>>();
        var result = await handler.Handle(
            new RequestLeaveCommand(employee.Id, parsedLeaveType, start, end, reason),
            cancellationToken);

        return result.IsError
            ? ToolHelpers.Failure(result.Errors)
            : $"Leave request submitted: {parsedLeaveType} {start.ToString(ToolHelpers.DateFormat)} to {end.ToString(ToolHelpers.DateFormat)} " +
              $"for {employee.FullName} ({employee.Email.Value}). Request id: {result.Value}. Status: Pending — awaiting a team manager's approval.";
    }

    [McpServerTool(Name = "cancel_leave_request")]
    [Description(
        "Cancels a leave request. Only the employee who submitted the request may cancel it. Pending requests " +
        "cancel freely; approved requests can only be cancelled before the leave starts.")]
    public static async Task<string> CancelLeaveRequest(
        [Description("Guid id of the leave request to cancel, as returned by request_leave or list_pending_approvals")] string requestId,
        [Description("Work email of the requesting employee (only the owner may cancel; a Guid employee id also works)")] string employeeEmail,
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var employees = provider.GetRequiredService<IEmployeeRepository>();
        var employee = await ToolHelpers.FindEmployeeAsync(employees, employeeEmail, cancellationToken);
        if (employee is null)
        {
            return ToolHelpers.UnknownEmployee(employeeEmail);
        }

        if (!ToolHelpers.TryParseRequestId(requestId, out var parsedRequestId))
        {
            return $"'requestId' must be a Guid (got '{requestId}'). Find ids via list_pending_approvals.";
        }

        var handler = provider.GetRequiredService<ICommandHandler<CancelLeaveRequestCommand>>();
        var result = await handler.Handle(
            new CancelLeaveRequestCommand(parsedRequestId, employee.Id),
            cancellationToken);

        return result.IsError
            ? ToolHelpers.Failure(result.Errors)
            : $"Leave request {parsedRequestId} cancelled for {employee.FullName} ({employee.Email.Value}).";
    }
}
