using System.ComponentModel;
using LeaveLite.Application.Abstractions;
using LeaveLite.Infrastructure.Initialization;
using LeaveLite.Server.Tools;
using ModelContextProtocol.Server;

namespace LeaveLite.Server.Tools;

/// <summary>Directory tools — the lookup data every AI caller needs before anything else.</summary>
[McpServerToolType]
public static class DirectoryTools
{
    [McpServerTool(Name = "list_employees")]
    [Description(
        "Lists every employee in the organization with their name, work email, team, role (Member/Manager), " +
        "hire date and accrual policy. Call this FIRST whenever you only know a person's name or need to find " +
        "who manages whom — every other tool identifies people by email.")]
    public static async Task<string> ListEmployees(
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        var employees = scope.ServiceProvider.GetRequiredService<IEmployeeRepository>();
        var policies = scope.ServiceProvider.GetRequiredService<IAccrualPolicyRepository>();

        var policyNames = (await policies.ListAsync(cancellationToken))
            .ToDictionary(policy => policy.Id, policy => policy.Name);

        var teamNames = DemoTeams.All.ToDictionary(team => team.Id, team => team.Name);

        var lines = new List<string>();
        foreach (var team in DemoTeams.All)
        {
            foreach (var employee in await employees.ListByTeamAsync(team.Id, cancellationToken))
            {
                lines.Add(
                    $"{employee.Email.Value} — {employee.FullName}, {employee.TeamRole} of the {teamNames[employee.TeamId]} team, " +
                    $"hired {employee.HiredOn.ToString(ToolHelpers.DateFormat)}, policy: {policyNames.GetValueOrDefault(employee.AccrualPolicyId, "<none>")}");
            }
        }

        return lines.Count == 0
            ? "No employees are enrolled yet."
            : $"Employee directory ({lines.Count}):\n" + string.Join("\n", lines);
    }

    [McpServerTool(Name = "who_reports_to_manager")]
    [Description(
        "Given a manager's work email, lists the employees that report to them (the members of their team, " +
        "excluding the manager). Use before approval workflows to see whose leave requests this manager can decide.")]
    public static async Task<string> WhoReportsToManager(
        [Description("Work email of the manager, e.g. ada@leavelite.io (a Guid id also works)")] string managerEmail,
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        var employees = scope.ServiceProvider.GetRequiredService<IEmployeeRepository>();
        var manager = await ToolHelpers.FindEmployeeAsync(employees, managerEmail, cancellationToken);
        if (manager is null)
        {
            return ToolHelpers.UnknownEmployee(managerEmail);
        }

        var reports = (await employees.ListByTeamAsync(manager.TeamId, cancellationToken))
            .Where(member => member.Id != manager.Id)
            .ToList();

        var teamName = DemoTeams.All.FirstOrDefault(team => team.Id == manager.TeamId)?.Name ?? "their";

        return reports.Count == 0
            ? $"{manager.FullName} ({manager.Email.Value}) has no direct reports."
            : $"{manager.FullName} manages the {teamName} team — {reports.Count} report(s):\n"
                + string.Join("\n", reports.Select(member =>
                    $"{member.Email.Value} — {member.FullName}, {member.TeamRole}, hired {member.HiredOn.ToString(ToolHelpers.DateFormat)}"));
    }
}
