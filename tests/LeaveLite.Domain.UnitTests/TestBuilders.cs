using LeaveLite.Domain.Employees;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.LeaveRequests;
using LeaveLite.Domain.Policies;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.UnitTests;

/// <summary>Shared builders for domain test data. All factories unwrap valid results —
/// invalid inputs are exercised explicitly in the dedicated factory tests.</summary>
internal static class TestEmployees
{
    public static readonly Guid PlatformTeamId = Guid.NewGuid();

    public static readonly AccrualPolicyId VacationPolicyId = AccrualPolicyId.New();

    public static Employee Standard(
        DateOnly hiredOn,
        EmploymentType employmentType = EmploymentType.FullTime,
        TeamRole teamRole = TeamRole.Member,
        AccrualPolicyId? policyId = null,
        string fullName = "Test Employee",
        string email = "employee@leavelite.io")
        => Employee.Create(fullName, email, employmentType, PlatformTeamId, teamRole, hiredOn, policyId ?? VacationPolicyId).Value;
}

internal static class TestPolicies
{
    public static AccrualPolicy MonthlyVacation(
        decimal hoursPerPeriod = 16m,
        int minTenureMonths = 3,
        decimal? annualCapHours = null,
        decimal? carryOverCapHours = null,
        EmploymentType employmentType = EmploymentType.FullTime)
        => AccrualPolicy.Create(
            "Vacation Monthly",
            LeaveType.Vacation,
            employmentType,
            AccrualPeriod.Monthly,
            hoursPerPeriod,
            annualCapHours,
            carryOverCapHours,
            minTenureMonths,
            grantsBalanceUpfront: false).Value;

    public static AccrualPolicy Yearly(
        LeaveType leaveType,
        decimal hoursPerPeriod,
        int minTenureMonths = 0,
        bool grantsBalanceUpfront = true,
        decimal? annualCapHours = null,
        EmploymentType employmentType = EmploymentType.FullTime)
        => AccrualPolicy.Create(
            $"{leaveType} Yearly",
            leaveType,
            employmentType,
            AccrualPeriod.Yearly,
            hoursPerPeriod,
            annualCapHours,
            carryOverCapHours: null,
            minTenureMonths,
            grantsBalanceUpfront).Value;
}

internal static class TestRequests
{
    public static readonly DateTimeOffset SubmittedAt = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset DecidedAt = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    public static LeaveRequest Pending(
        EmployeeId employeeId,
        DateOnly start,
        DateOnly end,
        LeaveType leaveType = LeaveType.Vacation,
        string? reason = null)
        => LeaveRequest.Create(employeeId, leaveType, DateRange.Create(start, end).Value, reason, SubmittedAt).Value;

    public static LeaveRequest Approved(
        EmployeeId employeeId,
        DateOnly start,
        DateOnly end,
        LeaveType leaveType = LeaveType.Vacation)
    {
        var request = Pending(employeeId, start, end, leaveType);
        request.Approve(EmployeeId.New(), DecidedAt);
        return request;
    }

    public static LeaveRequest Denied(
        EmployeeId employeeId,
        DateOnly start,
        DateOnly end,
        LeaveType leaveType = LeaveType.Vacation,
        string denialReason = "Not the right time")
    {
        var request = Pending(employeeId, start, end, leaveType);
        request.Deny(EmployeeId.New(), denialReason, DecidedAt);
        return request;
    }

    public static LeaveRequest Cancelled(EmployeeId employeeId, DateOnly start, DateOnly end)
    {
        var request = Pending(employeeId, start, end);
        request.Cancel(new DateOnly(2026, 8, 22));
        return request;
    }
}
