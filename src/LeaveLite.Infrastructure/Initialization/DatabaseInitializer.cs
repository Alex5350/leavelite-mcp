using ErrorOr;
using LeaveLite.Application.Abstractions;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Employees;
using LeaveLite.Domain.Holidays;
using LeaveLite.Domain.LeaveRequests;
using LeaveLite.Domain.Policies;
using LeaveLite.Domain.ValueObjects;
using LeaveLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeaveLite.Infrastructure.Initialization;

/// <summary>
/// Applies pending migrations and, on an empty database, seeds a demo organization.
/// Seeding computes all leave dates RELATIVE TO RUN TIME (via <see cref="IDateTimeProvider"/>)
/// so the demo data never goes stale — there is always a pending request next week and an
/// approved request next month, whatever day the server is started.
/// </summary>
public sealed class DatabaseInitializer(LeaveLiteDbContext context, IDateTimeProvider time)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await context.Database.MigrateAsync(cancellationToken);

        if (await context.Employees.AnyAsync(cancellationToken))
        {
            return; // Demo organization already seeded.
        }

        await SeedDemoOrganizationAsync(cancellationToken);
    }

    private async Task SeedDemoOrganizationAsync(CancellationToken cancellationToken)
    {
        var vacation = Unwrap(AccrualPolicy.Create(
            name: "Vacation Monthly 16h",
            leaveType: LeaveType.Vacation,
            employmentType: EmploymentType.FullTime,
            accrualPeriod: AccrualPeriod.Monthly,
            hoursPerPeriod: 16m,
            annualCapHours: null,
            carryOverCapHours: 40m,
            minTenureMonths: 3,
            grantsBalanceUpfront: false));

        var sick = Unwrap(AccrualPolicy.Create(
            name: "Sick Granted Upfront 40h",
            leaveType: LeaveType.Sick,
            employmentType: EmploymentType.FullTime,
            accrualPeriod: AccrualPeriod.Yearly,
            hoursPerPeriod: 40m,
            annualCapHours: null,
            carryOverCapHours: null,
            minTenureMonths: 0,
            grantsBalanceUpfront: true));

        var parental = Unwrap(AccrualPolicy.Create(
            name: "Parental Granted Upfront 160h",
            leaveType: LeaveType.Parental,
            employmentType: EmploymentType.FullTime,
            accrualPeriod: AccrualPeriod.Yearly,
            hoursPerPeriod: 160m,
            annualCapHours: null,
            carryOverCapHours: null,
            minTenureMonths: 0,
            grantsBalanceUpfront: true));

        context.AccrualPolicies.AddRange(vacation, sick, parental);

        var ada = Unwrap(Employee.Create("Ada Lovelace", "ada@leavelite.io", EmploymentType.FullTime, DemoTeams.PlatformId, TeamRole.Manager, new DateOnly(2023, 1, 9), vacation.Id));
        var bruno = Unwrap(Employee.Create("Bruno Chen", "bruno@leavelite.io", EmploymentType.FullTime, DemoTeams.PlatformId, TeamRole.Member, new DateOnly(2024, 3, 4), vacation.Id));
        var carla = Unwrap(Employee.Create("Carla Gomez", "carla@leavelite.io", EmploymentType.FullTime, DemoTeams.PlatformId, TeamRole.Member, new DateOnly(2025, 11, 17), vacation.Id));
        var dana = Unwrap(Employee.Create("Dana White", "dana@leavelite.io", EmploymentType.FullTime, DemoTeams.PlatformId, TeamRole.Member, new DateOnly(2024, 8, 15), sick.Id));
        var erin = Unwrap(Employee.Create("Erin Davis", "erin@leavelite.io", EmploymentType.FullTime, DemoTeams.PlatformId, TeamRole.Member, new DateOnly(2022, 6, 1), vacation.Id));

        // carla is the interesting hire: she passed the 3-month tenure gate this year, so her
        // balance is young and small — ideal for demonstrating the tenure gate and forecasts.
        context.Employees.AddRange(ada, bruno, carla, dana, erin);

        context.HolidayCalendars.AddRange(HolidayCalendarRow.FromDomain(HolidayCalendar.Create(2026, UsFederalHolidays2026)));

        SeedRelativeLeaveRequests(ada, bruno, carla, erin);

        await context.SaveChangesAsync(cancellationToken);
    }

    private void SeedRelativeLeaveRequests(Employee ada, Employee bruno, Employee carla, Employee erin)
    {
        var today = time.Today;

        // Next week's Monday (today if it is Monday is not used; always strictly upcoming).
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        var nextMonday = today.AddDays(daysUntilMonday == 0 ? 7 : daysUntilMonday);

        // First Monday of next month.
        var firstOfNextMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(1);
        while (firstOfNextMonth.DayOfWeek != DayOfWeek.Monday)
        {
            firstOfNextMonth = firstOfNextMonth.AddDays(1);
        }

        var now = time.UtcNow;

        // APPROVED: bruno takes the first full working week of next month.
        var brunoTrip = Unwrap(LeaveRequest.Create(bruno.Id, LeaveType.Vacation, Unwrap(DateRange.Create(firstOfNextMonth, firstOfNextMonth.AddDays(4))), "Family trip", now.AddDays(-5)));
        Unwrap(brunoTrip.Approve(ada.Id, now.AddDays(-4)));

        // PENDING: carla requests three days next week — awaits ada's decision.
        var carlaBreak = Unwrap(LeaveRequest.Create(carla.Id, LeaveType.Vacation, Unwrap(DateRange.Create(nextMonday, nextMonday.AddDays(2))), "Long weekend away", now.AddDays(-1)));

        // PENDING: erin requests two days the week after — a second item in ada's queue.
        var erinWedding = Unwrap(LeaveRequest.Create(erin.Id, LeaveType.Vacation, Unwrap(DateRange.Create(nextMonday.AddDays(14), nextMonday.AddDays(15))), "Wedding out of town", now.AddDays(-1)));

        context.LeaveRequests.AddRange(brunoTrip, carlaBreak, erinWedding);
    }

    /// <summary>US-federal-style company holidays for 2026 (observed dates; 10 entries).</summary>
    private static IEnumerable<Holiday> UsFederalHolidays2026 =>
    [
        new Holiday(new DateOnly(2026, 1, 1), "New Year's Day"),
        new Holiday(new DateOnly(2026, 1, 19), "Martin Luther King Jr. Day"),
        new Holiday(new DateOnly(2026, 2, 16), "Presidents' Day"),
        new Holiday(new DateOnly(2026, 5, 25), "Memorial Day"),
        new Holiday(new DateOnly(2026, 6, 19), "Juneteenth"),
        new Holiday(new DateOnly(2026, 7, 3), "Independence Day (Observed)"),
        new Holiday(new DateOnly(2026, 9, 7), "Labor Day"),
        new Holiday(new DateOnly(2026, 11, 11), "Veterans Day"),
        new Holiday(new DateOnly(2026, 11, 26), "Thanksgiving Day"),
        new Holiday(new DateOnly(2026, 12, 25), "Christmas Day"),
    ];

    /// <summary>Seed data is authored to be valid; an error here is a seed bug that must fail loudly.</summary>
    private static T Unwrap<T>(ErrorOr<T> result)
        => result.IsError ? throw new InvalidOperationException(string.Join("; ", result.Errors)) : result.Value;
}
