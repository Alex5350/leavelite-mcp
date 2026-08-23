using LeaveLite.Domain.Holidays;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.Common;

/// <summary>
/// The company-wide work schedule used by the accrual engine and staffing rules:
/// Monday-Friday working days, Saturday/Sunday weekends, holidays excluded via an
/// optional <see cref="HolidayCalendar"/>, and a constant full-day hour figure.
/// </summary>
public static class WorkSchedule
{
    /// <summary>Hours deducted from a balance for one working day of leave.</summary>
    public const decimal StandardHoursPerDay = 8m;

    /// <summary>Returns <c>true</c> when the date is a working day (Mon-Fri, not a holiday).</summary>
    public static bool IsWorkingDay(DateOnly date, HolidayCalendar? holidays)
        => !IsWeekend(date) && holidays?.IsHoliday(date) != true;

    /// <summary>Counts working days (Mon-Fri minus holidays) inside the range, boundaries included.</summary>
    public static int CountWorkingDays(DateRange range, HolidayCalendar? holidays)
    {
        var count = 0;
        for (var date = range.Start; date <= range.End; date = date.AddDays(1))
        {
            if (IsWorkingDay(date, holidays))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Working hours for the range: working days multiplied by <see cref="StandardHoursPerDay"/>.</summary>
    public static decimal WorkingHours(DateRange range, HolidayCalendar? holidays)
        => CountWorkingDays(range, holidays) * StandardHoursPerDay;

    private static bool IsWeekend(DateOnly date)
        => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}
