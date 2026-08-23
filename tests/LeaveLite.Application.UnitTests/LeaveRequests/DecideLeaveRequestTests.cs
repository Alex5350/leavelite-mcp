using ErrorOr;
using NSubstitute;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Common;
using LeaveLite.Application.LeaveRequests;
using LeaveLite.Domain.Common;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.LeaveRequests;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Application.UnitTests.LeaveRequests;

public sealed class DecideLeaveRequestTests : IAsyncDisposable
{
    private readonly ApplicationTestHost _host = new();

    private readonly Domain.Employees.Employee _ada; // Manager of the team
    private readonly Domain.Employees.Employee _bruno; // Member requesting leave
    private readonly Domain.Employees.Employee _carla; // Second member for staffing overrides
    private readonly Domain.Employees.Employee _outsiderManager; // Manager of another team

    public DecideLeaveRequestTests()
    {
        _ada = TestData.Employee("Ada Lovelace", "ada@leavelite.io", teamRole: TeamRole.Manager);
        _bruno = TestData.Employee("Bruno Chen", "bruno@leavelite.io");
        _carla = TestData.Employee("Carla Gomez", "carla@leavelite.io");
        _outsiderManager = TestData.Employee("Eve Other", "eve@other.io", teamRole: TeamRole.Manager, teamId: TestData.OtherTeamId);

        _host.Employees.GetByIdAsync(_ada.Id, Arg.Any<CancellationToken>()).Returns(_ada);
        _host.Employees.GetByIdAsync(_bruno.Id, Arg.Any<CancellationToken>()).Returns(_bruno);
        _host.Employees.GetByIdAsync(_outsiderManager.Id, Arg.Any<CancellationToken>()).Returns(_outsiderManager);
        _host.Employees.ListByTeamAsync(TestData.TeamId, Arg.Any<CancellationToken>())
            .Returns(new List<Domain.Employees.Employee> { _ada, _bruno });
        _host.LeaveRequests.ListByTeamAsync(
                TestData.TeamId, RequestStatus.Approved, Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Domain.LeaveRequests.LeaveRequest>());
    }

    private Domain.LeaveRequests.LeaveRequest ArrangePendingRequest()
    {
        var request = TestData.Pending(_bruno.Id, TestData.Weeks.PlainStart, TestData.Weeks.PlainEnd);
        _host.LeaveRequests.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        return request;
    }

    private Task<ErrorOr<Success>> Handle(DecideLeaveRequestCommand command)
        => _host.Handler<ICommandHandler<DecideLeaveRequestCommand>>().Handle(command, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Approve_HappyPath_ApprovesAndDispatchesApprovedEventAfterSave()
    {
        var request = ArrangePendingRequest();

        var result = await Handle(new DecideLeaveRequestCommand(request.Id, _ada.Id, Approve: true));

        Assert.False(result.IsError);
        Assert.Equal(RequestStatus.Approved, request.Status);
        Assert.Equal(_ada.Id, request.DecidedBy);
        await _host.Dispatcher.Received(1).DispatchAsync(
            Arg.Is<IReadOnlyCollection<IDomainEvent>>(events =>
                events.OfType<LeaveRequestApprovedDomainEvent>().Single().RequestId == request.Id),
            Arg.Any<CancellationToken>());

        // Decide order: persist first, then publish the pulled domain events.
        NSubstitute.Received.InOrder(async () =>
        {
            await _host.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            await _host.Dispatcher.DispatchAsync(Arg.Any<IReadOnlyCollection<IDomainEvent>>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Deny_HappyPath_DeniesWithReasonAndDispatchesDeniedEvent()
    {
        var request = ArrangePendingRequest();

        var result = await Handle(new DecideLeaveRequestCommand(request.Id, _ada.Id, Approve: false, DenialReason: "  Year-end freeze  "));

        Assert.False(result.IsError);
        Assert.Equal(RequestStatus.Denied, request.Status);
        Assert.Equal("Year-end freeze", request.DenialReason);
        await _host.Dispatcher.Received(1).DispatchAsync(
            Arg.Is<IReadOnlyCollection<IDomainEvent>>(events =>
                events.OfType<LeaveRequestDeniedDomainEvent>().Single().DenialReason == "Year-end freeze"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deny_WithoutReason_ReturnsValidationError()
    {
        // The FluentValidation rule gates the request before the aggregate can enforce its own rule.
        var request = ArrangePendingRequest();

        var result = await Handle(new DecideLeaveRequestCommand(request.Id, _ada.Id, Approve: false));

        Assert.True(result.IsError);
        Assert.Equal("DenialReason", result.FirstError.Code);
        Assert.Equal(RequestStatus.Pending, request.Status);
        await _host.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Approve_ByTeamMemberInsteadOfManager_ReturnsApproverNotTeamManager()
    {
        var request = ArrangePendingRequest();

        var result = await Handle(new DecideLeaveRequestCommand(request.Id, _bruno.Id, Approve: true));

        Assert.True(result.IsError);
        Assert.Equal("LeaveRequest.ApproverNotTeamManager", result.FirstError.Code);
        Assert.Equal(RequestStatus.Pending, request.Status);
        await _host.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Approve_ByManagerOfAnotherTeam_ReturnsApproverNotTeamManager()
    {
        var request = ArrangePendingRequest();

        var result = await Handle(new DecideLeaveRequestCommand(request.Id, _outsiderManager.Id, Approve: true));

        Assert.True(result.IsError);
        Assert.Equal("LeaveRequest.ApproverNotTeamManager", result.FirstError.Code);
    }

    [Fact]
    public async Task Approve_AlreadyDecidedRequest_ReturnsAlreadyDecided()
    {
        var request = ArrangePendingRequest();
        request.Approve(_ada.Id, ApplicationTestHost.UtcNow);

        var result = await Handle(new DecideLeaveRequestCommand(request.Id, _ada.Id, Approve: false, DenialReason: "Too late"));

        Assert.True(result.IsError);
        Assert.Equal("LeaveRequest.AlreadyDecided", result.FirstError.Code);
        Assert.Equal(RequestStatus.Approved, request.Status);
        await _host.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Approve_WhenNoTeammateStaysAvailable_ReturnsMinimumStaffingNotMet()
    {
        // Team is ada + bruno; the requester is excluded, so coverage = ada alone.
        // Ada herself is on approved leave for the whole range -> 1 - 1 = 0 < 1.
        var request = ArrangePendingRequest();
        var adaAway = TestData.Approved(_ada.Id, TestData.Weeks.PlainStart, TestData.Weeks.PlainEnd);
        _host.LeaveRequests.ListByTeamAsync(
                TestData.TeamId, RequestStatus.Approved, Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(new[] { adaAway });

        var result = await Handle(new DecideLeaveRequestCommand(request.Id, _ada.Id, Approve: true));

        Assert.True(result.IsError);
        Assert.Equal("LeaveRequest.MinimumStaffingNotMet", result.FirstError.Code);
        Assert.Contains("1 team member", result.FirstError.Description);
        Assert.Equal(RequestStatus.Pending, request.Status);
        await _host.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Approve_WithExplicitMinimumStaffTwoAndFullCoverage_Succeeds()
    {
        var request = ArrangePendingRequest();
        _host.Employees.ListByTeamAsync(TestData.TeamId, Arg.Any<CancellationToken>())
            .Returns(new List<Domain.Employees.Employee> { _ada, _bruno, _carla });

        var result = await Handle(new DecideLeaveRequestCommand(request.Id, _ada.Id, Approve: true, MinimumStaff: 2));

        Assert.False(result.IsError);
        Assert.Equal(RequestStatus.Approved, request.Status);
        await _host.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approve_WithExplicitMinimumStaffAboveCoverage_Blocks()
    {
        var request = ArrangePendingRequest();
        _host.Employees.ListByTeamAsync(TestData.TeamId, Arg.Any<CancellationToken>())
            .Returns(new List<Domain.Employees.Employee> { _ada, _bruno, _carla });

        var result = await Handle(new DecideLeaveRequestCommand(request.Id, _ada.Id, Approve: true, MinimumStaff: 3));

        Assert.True(result.IsError);
        Assert.Equal("LeaveRequest.MinimumStaffingNotMet", result.FirstError.Code);
        Assert.Equal(RequestStatus.Pending, request.Status);
    }

    [Fact]
    public async Task UnknownRequest_ReturnsNotFound()
    {
        var result = await Handle(new DecideLeaveRequestCommand(LeaveRequestId.New(), _ada.Id, Approve: true));

        Assert.True(result.IsError);
        Assert.Equal("LeaveRequest.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task UnknownApprover_ReturnsNotFound()
    {
        var request = ArrangePendingRequest();

        var result = await Handle(new DecideLeaveRequestCommand(request.Id, EmployeeId.New(), Approve: true));

        Assert.True(result.IsError);
        Assert.Equal("Employee.NotFound", result.FirstError.Code);
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
