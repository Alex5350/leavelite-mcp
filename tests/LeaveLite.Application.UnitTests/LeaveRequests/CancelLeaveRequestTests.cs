using ErrorOr;
using NSubstitute;
using LeaveLite.Application.Common;
using LeaveLite.Application.LeaveRequests;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Application.UnitTests.LeaveRequests;

public sealed class CancelLeaveRequestTests : IAsyncDisposable
{
    private readonly ApplicationTestHost _host = new();

    private readonly Domain.Employees.Employee _bruno = TestData.Employee("Bruno Chen", "bruno@leavelite.io");

    private Domain.LeaveRequests.LeaveRequest Arrange(Func<Domain.LeaveRequests.LeaveRequest> build)
    {
        var request = build();
        _host.LeaveRequests.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        return request;
    }

    private Task<ErrorOr<Success>> Handle(CancelLeaveRequestCommand command)
        => _host.Handler<ICommandHandler<CancelLeaveRequestCommand>>().Handle(command, TestContext.Current.CancellationToken);

    [Fact]
    public async Task PendingRequest_OwnerCancelsSuccessfully()
    {
        var request = Arrange(() => TestData.Pending(_bruno.Id, TestData.Weeks.PlainStart, TestData.Weeks.PlainEnd));

        var result = await Handle(new CancelLeaveRequestCommand(request.Id, _bruno.Id));

        Assert.False(result.IsError);
        Assert.Equal(RequestStatus.Cancelled, request.Status);
        await _host.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApprovedRequest_BeforeTheLeaveStarts_CancelsSuccessfully()
    {
        var request = Arrange(() => TestData.Approved(_bruno.Id, TestData.Weeks.PlainStart, TestData.Weeks.PlainEnd));

        var result = await Handle(new CancelLeaveRequestCommand(request.Id, _bruno.Id));

        Assert.False(result.IsError);
        Assert.Equal(RequestStatus.Cancelled, request.Status);
    }

    [Fact]
    public async Task ApprovedRequest_OnItsStartDay_ReturnsCannotCancelStarted()
    {
        var request = Arrange(() => TestData.Approved(_bruno.Id, ApplicationTestHost.Today, TestData.Weeks.PlainEnd));

        var result = await Handle(new CancelLeaveRequestCommand(request.Id, _bruno.Id));

        Assert.True(result.IsError);
        Assert.Equal("LeaveRequest.CannotCancelStarted", result.FirstError.Code);
        Assert.Equal(RequestStatus.Approved, request.Status);
        await _host.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RequestBySomeoneElse_ReturnsNotOwner()
    {
        var request = Arrange(() => TestData.Pending(_bruno.Id, TestData.Weeks.PlainStart, TestData.Weeks.PlainEnd));

        var result = await Handle(new CancelLeaveRequestCommand(request.Id, EmployeeId.New()));

        Assert.True(result.IsError);
        Assert.Equal("LeaveRequest.NotOwner", result.FirstError.Code);
        Assert.Equal(RequestStatus.Pending, request.Status);
        await _host.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnknownRequest_ReturnsNotFound()
    {
        var result = await Handle(new CancelLeaveRequestCommand(LeaveRequestId.New(), _bruno.Id));

        Assert.True(result.IsError);
        Assert.Equal("LeaveRequest.NotFound", result.FirstError.Code);
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
