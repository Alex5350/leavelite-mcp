using LeaveLite.Application.Abstractions;
using LeaveLite.Domain.Holidays;
using LeaveLite.Domain.LeaveRequests;

namespace LeaveLite.Application.Common;

/// <summary>
/// Loads and merges the per-year holiday calendars covering all years a computation touches
/// (the as-of date, the forecast horizon, and the date ranges of approved leave).
/// </summary>
internal static class HolidaySupport
{
    public static async Task<HolidayCalendar?> LoadAsync(
        IHolidayCalendarRepository repository,
        IEnumerable<int> years,
        CancellationToken cancellationToken)
    {
        List<HolidayCalendar> calendars = [];

        foreach (var year in years.Distinct())
        {
            if (await repository.GetAsync(year, cancellationToken) is { } calendar)
            {
                calendars.Add(calendar);
            }
        }

        return calendars.Count switch
        {
            0 => null,
            1 => calendars[0],
            _ => HolidayCalendar.Combine(calendars),
        };
    }

    public static IEnumerable<int> YearsCoveredBy(DateOnly from, DateOnly to)
    {
        List<int> years = [];
        for (var year = from.Year; year <= to.Year; year++)
        {
            years.Add(year);
        }

        return years;
    }

    /// <summary>All years relevant to a balance computation: the [from, to] window plus every request's range.</summary>
    public static IEnumerable<int> CollectCoveredYears(
        IEnumerable<LeaveRequest> history,
        DateOnly from,
        DateOnly to)
    {
        var years = new List<int>(YearsCoveredBy(from, to));

        foreach (var request in history)
        {
            years.AddRange(YearsCoveredBy(request.DateRange.Start, request.DateRange.End));
        }

        return years;
    }
}
