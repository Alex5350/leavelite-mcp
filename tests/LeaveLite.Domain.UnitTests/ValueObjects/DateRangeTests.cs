using LeaveLite.Domain.Errors;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.UnitTests.ValueObjects;

public sealed class DateRangeTests
{
    private static readonly DateOnly Jan1 = new(2026, 1, 1);
    private static readonly DateOnly Jan31 = new(2026, 1, 31);

    [Fact]
    public void TryCreate_StartBeforeOrOnEnd_Succeeds()
    {
        var created = DateRange.TryCreate(Jan1, Jan31, out var range);

        Assert.True(created);
        Assert.NotNull(range);
        Assert.Equal(Jan1, range.Start);
        Assert.Equal(Jan31, range.End);
    }

    [Fact]
    public void TryCreate_SingleDay_Succeeds()
    {
        var created = DateRange.TryCreate(Jan31, Jan31, out var range);

        Assert.True(created);
        Assert.Equal(Jan31, range!.Start);
        Assert.Equal(Jan31, range.End);
    }

    [Fact]
    public void TryCreate_StartAfterEnd_FailsWithNullRange()
    {
        var created = DateRange.TryCreate(Jan31, Jan1, out var range);

        Assert.False(created);
        Assert.Null(range);
    }

    [Fact]
    public void Create_StartAfterEnd_ReturnsStartAfterEndError()
    {
        var result = DateRange.Create(Jan31, Jan1);

        Assert.True(result.IsError);
        Assert.Equal(DateRangeErrors.StartAfterEnd(Jan31, Jan1).Code, result.FirstError.Code);
    }

    [Fact]
    public void Create_ValidRange_ReturnsRange()
    {
        var result = DateRange.Create(Jan1, Jan31);

        Assert.False(result.IsError);
        Assert.Equal(Jan1, result.Value.Start);
        Assert.Equal(Jan31, result.Value.End);
    }

    [Theory]
    [InlineData("2026-01-01", "2026-01-01", 1)] // single day counts as one day
    [InlineData("2026-01-01", "2026-01-31", 31)] // inclusive boundaries
    [InlineData("2026-01-31", "2026-02-01", 2)] // month boundary
    [InlineData("2026-12-31", "2027-01-01", 2)] // year boundary
    public void NumberOfDays_IsInclusiveOfBothBoundaries(string start, string end, int expected)
    {
        var range = DateRange.Create(DateOnly.Parse(start), DateOnly.Parse(end)).Value;

        Assert.Equal(expected, range.NumberOfDays);
    }

    [Theory]
    [InlineData("2026-01-01", true)] // start boundary
    [InlineData("2026-01-15", true)] // inside
    [InlineData("2026-01-31", true)] // end boundary
    [InlineData("2025-12-31", false)] // before
    [InlineData("2026-02-01", false)] // after
    public void Contains_CoversBoundariesInclusively(string date, bool expected)
    {
        var range = DateRange.Create(Jan1, Jan31).Value;

        Assert.Equal(expected, range.Contains(DateOnly.Parse(date)));
    }

    [Theory]
    [InlineData("2026-01-01", "2026-01-31", "2026-01-31", "2026-02-10", true)] // touching at a boundary shares a day
    [InlineData("2026-01-01", "2026-01-31", "2026-01-10", "2026-01-20", true)] // nested
    [InlineData("2026-01-01", "2026-01-31", "2026-02-01", "2026-02-10", false)] // strictly after
    [InlineData("2026-02-01", "2026-02-10", "2026-01-01", "2026-01-31", false)] // strictly before
    [InlineData("2026-01-01", "2026-01-31", "2026-01-01", "2026-01-31", true)] // identical
    [InlineData("2026-01-15", "2026-01-15", "2026-01-01", "2026-01-31", true)] // single day inside
    public void Overlaps_DetectsSharedCalendarDays(string firstStart, string firstEnd, string secondStart, string secondEnd, bool expected)
    {
        var first = DateRange.Create(DateOnly.Parse(firstStart), DateOnly.Parse(firstEnd)).Value;
        var second = DateRange.Create(DateOnly.Parse(secondStart), DateOnly.Parse(secondEnd)).Value;

        Assert.Equal(expected, first.Overlaps(second));
        Assert.Equal(!expected, first.DisjointWith(second));
    }

    [Fact]
    public void ToString_RendersIsoDatesSeparatedByTwoDots()
    {
        var range = DateRange.Create(Jan1, Jan31).Value;

        Assert.Equal("2026-01-01..2026-01-31", range.ToString());
    }

    [Fact]
    public void Equality_SameBoundariesAreEqualRecords()
    {
        var first = DateRange.Create(Jan1, Jan31).Value;
        var second = DateRange.Create(Jan1, Jan31).Value;

        Assert.Equal(first, second);
    }
}
