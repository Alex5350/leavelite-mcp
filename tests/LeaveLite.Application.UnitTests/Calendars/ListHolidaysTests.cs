using ErrorOr;
using NSubstitute;
using LeaveLite.Application.Calendars;
using LeaveLite.Application.Common;

namespace LeaveLite.Application.UnitTests.Calendars;

public sealed class ListHolidaysTests : IAsyncDisposable
{
    private readonly ApplicationTestHost _host = new();

    private Task<ErrorOr<IReadOnlyList<HolidayDto>>> Handle(ListHolidaysQuery query)
        => _host.Handler<IQueryHandler<ListHolidaysQuery, IReadOnlyList<HolidayDto>>>().Handle(query, TestContext.Current.CancellationToken);

    [Fact]
    public async Task HappyPath_ReturnsHolidaysSortedByDate()
    {
        _host.HolidayCalendars.GetAsync(2026, Arg.Any<CancellationToken>()).Returns(TestData.Us2026());

        var result = await Handle(new ListHolidaysQuery(2026));

        Assert.False(result.IsError);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Value[0].Date);
        Assert.Equal("Labor Day", result.Value[0].Name);
        Assert.Equal(new DateOnly(2026, 11, 26), result.Value[1].Date);
        Assert.Equal("Thanksgiving Day", result.Value[1].Name);
    }

    [Fact]
    public async Task YearWithoutCalendar_ReturnsEmptyList()
    {
        _host.HolidayCalendars.GetAsync(2030, Arg.Any<CancellationToken>()).Returns((Domain.Holidays.HolidayCalendar?)null);

        var result = await Handle(new ListHolidaysQuery(2030));

        Assert.False(result.IsError);
        Assert.Empty(result.Value);
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2201)]
    public async Task YearOutsideBounds_ReturnsValidationError(int year)
    {
        var result = await Handle(new ListHolidaysQuery(year));

        Assert.True(result.IsError);
        Assert.Equal("Year", result.FirstError.Code);
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
