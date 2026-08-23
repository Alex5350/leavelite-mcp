using System.Diagnostics.CodeAnalysis;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.Holidays;

/// <summary>
/// The set of named holidays for a year (a value-ish entity persisted as configuration).
/// <see cref="Year"/> is identity metadata; membership checks use only the date set,
/// so calendars covering several years can be merged via <see cref="Combine"/> for
/// multi-year balance computations.
/// </summary>
public sealed class HolidayCalendar
{
    private readonly Dictionary<DateOnly, string> _holidays;

    private HolidayCalendar(Guid id, int year, Dictionary<DateOnly, string> holidays)
    {
        Id = id;
        Year = year;
        _holidays = holidays;
    }

    public Guid Id { get; }

    public int Year { get; }

    public IReadOnlyCollection<Holiday> Holidays
        => _holidays.Select(static pair => new Holiday(pair.Key, pair.Value)).ToList();

    public bool IsHoliday(DateOnly date) => _holidays.ContainsKey(date);

    public bool TryGetHoliday(DateOnly date, [NotNullWhen(true)] out string? name)
        => _holidays.TryGetValue(date, out name);

    /// <summary>Counts Mon-Fri days in the range minus holidays falling inside it.</summary>
    public int WorkingDaysInRange(DateRange range)
    {
        var count = 0;
        for (var date = range.Start; date <= range.End; date = date.AddDays(1))
        {
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !IsHoliday(date))
            {
                count++;
            }
        }

        return count;
    }

    public static HolidayCalendar Create(int year, IEnumerable<Holiday> holidays)
    {
        var map = new Dictionary<DateOnly, string>();
        foreach (var holiday in holidays)
        {
            map[holiday.Date] = holiday.Name.Trim();
        }

        return new HolidayCalendar(Guid.CreateVersion7(), year, map);
    }

    /// <summary>Merges calendars (typically one per year) into a single multi-year calendar.</summary>
    public static HolidayCalendar Combine(IEnumerable<HolidayCalendar> calendars)
    {
        var list = calendars.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("At least one calendar is required.", nameof(calendars));
        }

        var merged = new Dictionary<DateOnly, string>();
        foreach (var calendar in list)
        {
            foreach (var (date, name) in calendar._holidays)
            {
                merged[date] = name;
            }
        }

        var earliestYear = list.Min(static calendar => calendar.Year);
        return new HolidayCalendar(Guid.CreateVersion7(), earliestYear, merged);
    }
}
