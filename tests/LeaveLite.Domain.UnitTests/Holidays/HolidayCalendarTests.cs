using LeaveLite.Domain.Holidays;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.UnitTests.Holidays;

public sealed class HolidayCalendarTests
{
    private static readonly DateOnly LaborDay2026 = new(2026, 9, 7); // a Monday
    private static readonly DateOnly Thanksgiving2026 = new(2026, 11, 26); // a Thursday

    private static HolidayCalendar Us2026()
        => HolidayCalendar.Create(2026, [new Holiday(LaborDay2026, "Labor Day"), new Holiday(Thanksgiving2026, "Thanksgiving Day")]);

    [Fact]
    public void Create_StoresYearAndHolidays()
    {
        var calendar = Us2026();

        Assert.Equal(2026, calendar.Year);
        Assert.Equal(2, calendar.Holidays.Count);
        Assert.Contains(calendar.Holidays, holiday => holiday.Name == "Labor Day" && holiday.Date == LaborDay2026);
        Assert.NotEqual(Guid.Empty, calendar.Id);
    }

    [Fact]
    public void Create_TrimsHolidayNames()
    {
        var calendar = HolidayCalendar.Create(2026, [new Holiday(LaborDay2026, "  Labor Day  ")]);

        var holiday = Assert.Single(calendar.Holidays);
        Assert.Equal("Labor Day", holiday.Name);
    }

    [Fact]
    public void Create_DuplicateDates_KeepTheLastName()
    {
        var calendar = HolidayCalendar.Create(2026, [new(LaborDay2026, "Labor Day"), new(LaborDay2026, "Replacement Day")]);

        Assert.Single(calendar.Holidays);
        Assert.Equal("Replacement Day", calendar.Holidays.Single().Name);
    }

    [Fact]
    public void IsHoliday_MatchesConfiguredDatesOnly()
    {
        var calendar = Us2026();

        Assert.True(calendar.IsHoliday(LaborDay2026));
        Assert.True(calendar.IsHoliday(Thanksgiving2026));
        Assert.False(calendar.IsHoliday(new DateOnly(2026, 9, 8)));
    }

    [Fact]
    public void TryGetHoliday_ReturnsNameForKnownDateAndFailsOtherwise()
    {
        var calendar = Us2026();

        var known = calendar.TryGetHoliday(LaborDay2026, out var name);
        var unknown = calendar.TryGetHoliday(new DateOnly(2026, 12, 24), out var missing);

        Assert.True(known);
        Assert.Equal("Labor Day", name);
        Assert.False(unknown);
        Assert.Null(missing);
    }

    [Theory]
    [InlineData("2026-09-07", "2026-09-13", 4)]  // full week with the Monday holiday
    [InlineData("2026-09-08", "2026-09-11", 4)]  // Tue-Fri, no weekend, no holiday
    [InlineData("2026-09-12", "2026-09-13", 0)]  // weekend only
    [InlineData("2026-09-07", "2026-09-07", 0)]  // single holiday Monday
    [InlineData("2026-09-11", "2026-09-15", 3)]  // Fri + weekend + Tue,Weds (Monday holiday outside)
    public void WorkingDaysInRange_ExcludesWeekendsAndHolidays(string from, string to, int expected)
    {
        var calendar = Us2026();
        var range = DateRange.Create(DateOnly.Parse(from), DateOnly.Parse(to)).Value;

        Assert.Equal(expected, calendar.WorkingDaysInRange(range));
    }

    [Fact]
    public void WorkingDaysInRange_WeekWithoutHolidays_CountsMondayToFriday()
    {
        var calendar = HolidayCalendar.Create(2026, []); // no holidays at all
        var range = DateRange.Create(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20)).Value; // Mon-Sun

        Assert.Equal(5, calendar.WorkingDaysInRange(range));
    }

    [Fact]
    public void WorkingDaysInRange_AllDaysHolidays_ReturnsZero()
    {
        var calendar = HolidayCalendar.Create(2026, [new(new DateOnly(2026, 9, 8), "A"), new(new DateOnly(2026, 9, 9), "B"), new(new DateOnly(2026, 9, 10), "C")]);
        var range = DateRange.Create(new DateOnly(2026, 9, 8), new DateOnly(2026, 9, 10)).Value; // Tue-Thu

        Assert.Equal(0, calendar.WorkingDaysInRange(range));
    }

    [Fact]
    public void Combine_MergesCalendarsAcrossYears()
    {
        var first = HolidayCalendar.Create(2026, [new(LaborDay2026, "Labor Day")]);
        var second = HolidayCalendar.Create(2027, [new(new DateOnly(2027, 1, 1), "New Year's Day")]);

        var merged = HolidayCalendar.Combine([first, second]);

        Assert.Equal(2026, merged.Year); // earliest year wins
        Assert.Equal(2, merged.Holidays.Count);
        Assert.True(merged.IsHoliday(LaborDay2026));
        Assert.True(merged.IsHoliday(new DateOnly(2027, 1, 1)));
        Assert.False(merged.IsHoliday(new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void Combine_LaterCalendarWinsOnDuplicateDates()
    {
        var early = HolidayCalendar.Create(2026, [new(LaborDay2026, "Labor Day")]);
        var late = HolidayCalendar.Create(2027, [new(LaborDay2026, "Observed carry-over")]);

        var merged = HolidayCalendar.Combine([early, late]);

        Assert.True(merged.TryGetHoliday(LaborDay2026, out var name));
        Assert.Equal("Observed carry-over", name);
    }

    [Fact]
    public void Combine_EmptyList_Throws()
    {
        Assert.Throws<ArgumentException>(() => HolidayCalendar.Combine([]));
    }

    [Fact]
    public void WorkingDaysInRange_MatchesWorkScheduleCount()
    {
        var calendar = Us2026();
        var range = DateRange.Create(new DateOnly(2026, 9, 4), new DateOnly(2026, 9, 11)).Value; // Fri -> Fri

        // Both implementations must agree: Sep 4(Fri),7(holiday),8,9,10,11 minus weekend Sep 5-6.
        Assert.Equal(5, calendar.WorkingDaysInRange(range));
        Assert.Equal(5, Domain.Common.WorkSchedule.CountWorkingDays(range, calendar));
    }
}
