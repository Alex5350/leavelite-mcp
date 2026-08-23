using ErrorOr;
using NSubstitute;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Calendars;
using LeaveLite.Application.Common;
using LeaveLite.Domain.Enums;

namespace LeaveLite.Application.UnitTests.Calendars;

public sealed class GetTeamCalendarTests : IAsyncDisposable
{
    private readonly ApplicationTestHost _host = new();

    private readonly Domain.Employees.Employee _bruno = TestData.Employee("Bruno Chen", "bruno@leavelite.io");

    public GetTeamCalendarTests()
    {
        _host.Employees.ListByTeamAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Domain.Employees.Employee> { _bruno });
        _host.LeaveRequests.ListByTeamAsync(
                Arg.Any<Guid>(), RequestStatus.Approved, Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Domain.LeaveRequests.LeaveRequest>());
    }

    private Task<ErrorOr<IReadOnlyList<TeamCalendarEntry>>> Handle(GetTeamCalendarQuery query)
        => _host.Handler<IQueryHandler<GetTeamCalendarQuery, IReadOnlyList<TeamCalendarEntry>>>().Handle(query, TestContext.Current.CancellationToken);

    [Fact]
    public async Task HappyPath_MarksHolidaysWeekendsAndEmployeesOnLeave()
    {
        _host.HolidayCalendars.GetAsync(2026, Arg.Any<CancellationToken>()).Returns(TestData.Us2026());
        _host.LeaveRequests.ListByTeamAsync(
                TestData.TeamId, RequestStatus.Approved, Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(new[] { TestData.Approved(_bruno.Id, TestData.Weeks.HolidayStart, TestData.Weeks.HolidayEnd) });

        var result = await Handle(new GetTeamCalendarQuery(TestData.TeamId, new DateOnly(2026, 9, 5), new DateOnly(2026, 9, 9)));

        Assert.False(result.IsError);
        var entries = result.Value;
        Assert.Equal(5, entries.Count);

        Assert.All(entries, entry => Assert.Equal(entry.Date.DayOfWeek, entry.Weekday));

        var laborDay = entries.Single(entry => entry.Date == new DateOnly(2026, 9, 7));
        Assert.Equal("Labor Day", laborDay.HolidayName);
        Assert.False(laborDay.IsWorkingDay);
        Assert.Contains("Bruno Chen", laborDay.EmployeesOnLeave);

        var saturday = entries.Single(entry => entry.Date == new DateOnly(2026, 9, 5));
        Assert.Null(saturday.HolidayName);
        Assert.False(saturday.IsWorkingDay);

        var tuesday = entries.Single(entry => entry.Date == new DateOnly(2026, 9, 8));
        Assert.Null(tuesday.HolidayName);
        Assert.True(tuesday.IsWorkingDay);
        Assert.Contains("Bruno Chen", tuesday.EmployeesOnLeave);
    }

    [Fact]
    public async Task DaysOutsideApprovedLeave_ShowNobodyAway()
    {
        _host.LeaveRequests.ListByTeamAsync(
                TestData.TeamId, RequestStatus.Approved, Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(new[] { TestData.Approved(_bruno.Id, TestData.Weeks.PlainStart, TestData.Weeks.PlainEnd) });

        var result = await Handle(new GetTeamCalendarQuery(TestData.TeamId, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 7)));

        Assert.False(result.IsError);
        Assert.All(result.Value, entry => Assert.Empty(entry.EmployeesOnLeave));
    }

    [Fact]
    public async Task RangeOfExactly62Days_IsAllowed()
    {
        var from = new DateOnly(2026, 9, 1);
        var to = from.AddDays(61);

        var result = await Handle(new GetTeamCalendarQuery(TestData.TeamId, from, to));

        Assert.False(result.IsError);
        Assert.Equal(62, result.Value.Count);
    }

    [Fact]
    public async Task RangeOf63Days_IsRejectedByTheValidator()
    {
        var from = new DateOnly(2026, 9, 1);
        var to = from.AddDays(62);

        var result = await Handle(new GetTeamCalendarQuery(TestData.TeamId, from, to));

        Assert.True(result.IsError);
        Assert.Contains("62", result.FirstError.Description);
        await _host.Employees.DidNotReceiveWithAnyArgs().ListByTeamAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EndBeforeStart_IsRejectedWithEndPropertyCode()
    {
        var result = await Handle(new GetTeamCalendarQuery(TestData.TeamId, new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 1)));

        Assert.True(result.IsError);
        Assert.Equal("To", result.FirstError.Code);
    }

    [Fact]
    public async Task EmptyTeamId_IsRejectedByTheValidator()
    {
        var result = await Handle(new GetTeamCalendarQuery(Guid.Empty, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10)));

        Assert.True(result.IsError);
        Assert.Equal("TeamId", result.FirstError.Code);
    }

    [Fact]
    public async Task ForMonth_ExpandsToTheWholeMonth()
    {
        var query = GetTeamCalendarQuery.ForMonth(TestData.TeamId, new DateOnly(2026, 2, 14));

        Assert.Equal(new DateOnly(2026, 2, 1), query.From);
        Assert.Equal(new DateOnly(2026, 2, 28), query.To);

        var result = await Handle(query);

        Assert.False(result.IsError);
        Assert.Equal(28, result.Value.Count);
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
