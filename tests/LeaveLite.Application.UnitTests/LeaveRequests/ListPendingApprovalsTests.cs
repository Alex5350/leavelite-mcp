using ErrorOr;
using NSubstitute;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Common;
using LeaveLite.Application.LeaveRequests;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Application.UnitTests.LeaveRequests;

public sealed class ListPendingApprovalsTests : IAsyncDisposable
{
    private readonly ApplicationTestHost _host = new();

    private readonly Domain.Employees.Employee _ada = TestData.Employee("Ada Lovelace", "ada@leavelite.io", teamRole: TeamRole.Manager);

    private readonly Domain.Employees.Employee _bruno;

    public ListPendingApprovalsTests()
    {
        _bruno = TestData.Employee("Bruno Chen", "bruno@leavelite.io");
        _host.Employees.GetByIdAsync(_ada.Id, Arg.Any<CancellationToken>()).Returns(_ada);
        _host.Employees.ListByTeamAsync(TestData.TeamId, Arg.Any<CancellationToken>())
            .Returns(new List<Domain.Employees.Employee> { _ada, _bruno });
        _host.LeaveRequests.ListByTeamAsync(
                TestData.TeamId, RequestStatus.Pending, Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Domain.LeaveRequests.LeaveRequest>());
    }

    private Task<ErrorOr<IReadOnlyList<PendingApprovalItem>>> Handle(ListPendingApprovalsQuery query)
        => _host.Handler<IQueryHandler<ListPendingApprovalsQuery, IReadOnlyList<PendingApprovalItem>>>().Handle(query, TestContext.Current.CancellationToken);

    [Fact]
    public async Task HappyPath_ListsPendingRequestsWithComputedWorkingDays()
    {
        var pending = TestData.Pending(_bruno.Id, TestData.Weeks.PlainStart, TestData.Weeks.PlainEnd, reason: "Family trip");
        _host.LeaveRequests.ListByTeamAsync(
                TestData.TeamId, RequestStatus.Pending, Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(new[] { pending });

        var result = await Handle(new ListPendingApprovalsQuery(_ada.Id));

        Assert.False(result.IsError);
        var item = Assert.Single(result.Value);
        Assert.Equal(pending.Id, item.RequestId);
        Assert.Equal("Bruno Chen", item.EmployeeName);
        Assert.Equal(LeaveType.Vacation, item.LeaveType);
        Assert.Equal(TestData.Weeks.PlainStart, item.Start);
        Assert.Equal(TestData.Weeks.PlainEnd, item.End);
        Assert.Equal(5, item.WorkingDays);
        Assert.Equal(40m, item.RequestedHours);
        Assert.Equal("Family trip", item.Reason);
    }

    [Fact]
    public async Task HappyPath_HolidaysReduceWorkingDaysAndHours()
    {
        var pending = TestData.Pending(_bruno.Id, TestData.Weeks.HolidayStart, TestData.Weeks.HolidayEnd);
        _host.LeaveRequests.ListByTeamAsync(
                TestData.TeamId, RequestStatus.Pending, Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(new[] { pending });
        _host.HolidayCalendars.GetAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(TestData.Us2026());

        var result = await Handle(new ListPendingApprovalsQuery(_ada.Id));

        Assert.False(result.IsError);
        var item = Assert.Single(result.Value);
        Assert.Equal(4, item.WorkingDays); // Mon-Fri minus Labor Day
        Assert.Equal(32m, item.RequestedHours);
    }

    [Fact]
    public async Task EmptyQueue_ReturnsEmptyList()
    {
        var result = await Handle(new ListPendingApprovalsQuery(_ada.Id));

        Assert.False(result.IsError);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task RequesterIsNotAManager_ReturnsNotAManager()
    {
        var brunoAsRequester = _bruno;
        _host.Employees.GetByIdAsync(brunoAsRequester.Id, Arg.Any<CancellationToken>()).Returns(brunoAsRequester);

        var result = await Handle(new ListPendingApprovalsQuery(brunoAsRequester.Id));

        Assert.True(result.IsError);
        Assert.Equal("Employee.NotAManager", result.FirstError.Code);
    }

    [Fact]
    public async Task UnknownManager_ReturnsNotFound()
    {
        var result = await Handle(new ListPendingApprovalsQuery(EmployeeId.New()));

        Assert.True(result.IsError);
        Assert.Equal("Employee.NotFound", result.FirstError.Code);
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
