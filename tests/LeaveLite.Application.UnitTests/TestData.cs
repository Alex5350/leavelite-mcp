using LeaveLite.Domain.Employees;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Holidays;
using LeaveLite.Domain.LeaveRequests;
using LeaveLite.Domain.Policies;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Application.UnitTests;

/// <summary>Shared domain test data for handler tests (mirrors the domain suite's builders).</summary>
internal static class TestData
{
    public static readonly Guid TeamId = Guid.NewGuid();

    public static readonly Guid OtherTeamId = Guid.NewGuid();

    /// <summary>The 2026 company calendar: Labor Day Monday Sep 7 + Thanksgiving Thursday Nov 26.</summary>
    public static HolidayCalendar Us2026()
        => HolidayCalendar.Create(
            2026,
            [new Holiday(new DateOnly(2026, 9, 7), "Labor Day"), new Holiday(new DateOnly(2026, 11, 26), "Thanksgiving Day")]);

    public static AccrualPolicy VacationMonthly(
        decimal hoursPerPeriod = 16m,
        int minTenureMonths = 3,
        decimal? annualCapHours = null)
        => AccrualPolicy.Create(
            "Vacation Monthly",
            LeaveType.Vacation,
            EmploymentType.FullTime,
            AccrualPeriod.Monthly,
            hoursPerPeriod,
            annualCapHours,
            carryOverCapHours: null,
            minTenureMonths,
            grantsBalanceUpfront: false).Value;

    public static AccrualPolicy SickUpfront(decimal hours = 40m, EmploymentType employmentType = EmploymentType.FullTime)
        => AccrualPolicy.Create(
            "Sick Granted Upfront",
            LeaveType.Sick,
            employmentType,
            AccrualPeriod.Yearly,
            hours,
            annualCapHours: null,
            carryOverCapHours: null,
            minTenureMonths: 0,
            grantsBalanceUpfront: true).Value;

    public static Employee Employee(
        string fullName,
        string email,
        TeamRole teamRole = TeamRole.Member,
        Guid? teamId = null,
        DateOnly? hiredOn = null,
        EmploymentType employmentType = EmploymentType.FullTime,
        AccrualPolicyId? policyId = null)
        => Domain.Employees.Employee.Create(
            fullName,
            email,
            employmentType,
            teamId ?? TeamId,
            teamRole,
            hiredOn ?? new DateOnly(2026, 1, 5),
            policyId ?? VacationMonthly().Id).Value;

    public static LeaveRequest Pending(EmployeeId employeeId, DateOnly start, DateOnly end, LeaveType leaveType = LeaveType.Vacation, string? reason = null)
        => LeaveRequest.Create(employeeId, leaveType, DateRange.Create(start, end).Value, reason, ApplicationTestHost.UtcNow).Value;

    public static LeaveRequest Approved(EmployeeId employeeId, DateOnly start, DateOnly end, LeaveType leaveType = LeaveType.Vacation)
    {
        var request = Pending(employeeId, start, end, leaveType);
        request.Approve(EmployeeId.New(), ApplicationTestHost.UtcNow);
        return request;
    }

    public static class Weeks
    {
        /// <summary>Mon-Fri week in September 2026 with no holidays.</summary>
        public static readonly DateOnly PlainStart = new(2026, 9, 14);

        public static readonly DateOnly PlainEnd = new(2026, 9, 18);

        /// <summary>Mon-Fri week containing Labor Day (Mon 2026-09-07).</summary>
        public static readonly DateOnly HolidayStart = new(2026, 9, 7);

        public static readonly DateOnly HolidayEnd = new(2026, 9, 11);

        /// <summary>Next Monday after the frozen today (Saturday 2026-08-22).</summary>
        public static readonly DateOnly NextMonday = new(2026, 8, 24);

        /// <summary>The Tuesday after <see cref="NextMonday"/>.</summary>
        public static readonly DateOnly NextTuesday = new(2026, 8, 25);
    }
}
