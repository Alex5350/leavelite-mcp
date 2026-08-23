using ErrorOr;
using NSubstitute;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Common;
using LeaveLite.Application.LeaveRequests;
using LeaveLite.Domain.Balances;
using LeaveLite.Domain.Common;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Policies;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Application.UnitTests.LeaveRequests;

public sealed class RequestLeaveTests : IAsyncDisposable
{
    private readonly ApplicationTestHost _host = new();

    private readonly AccrualPolicy _vacationPolicy = TestData.VacationMonthly();

    private readonly Domain.Employees.Employee _bruno;

    public RequestLeaveTests()
    {
        _bruno = TestData.Employee("Bruno Chen", "bruno@leavelite.io", policyId: _vacationPolicy.Id);
        _host.Employees.GetByIdAsync(_bruno.Id, Arg.Any<CancellationToken>()).Returns(_bruno);
        _host.Policies.GetByIdAsync(_vacationPolicy.Id, Arg.Any<CancellationToken>()).Returns(_vacationPolicy);
        _host.LeaveRequests.GetOverlappingAsync(Arg.Any<EmployeeId>(), Arg.Any<Domain.ValueObjects.DateRange>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Domain.LeaveRequests.LeaveRequest>());
        _host.LeaveRequests.ListByEmployeeAsync(_bruno.Id, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Domain.LeaveRequests.LeaveRequest>());
    }

    private Task<ErrorOr<LeaveRequestId>> Handle(RequestLeaveCommand command)
        => _host.Handler<ICommandHandler<RequestLeaveCommand, LeaveRequestId>>().Handle(command, TestContext.Current.CancellationToken);

    [Fact]
    public async Task HappyPath_SubmitsPendingRequestWithoutLowBalanceWarning()
    {
        // Frozen-today balance is 121.29h; a 5-day week costs 40h, leaving 81.29h >= 8h threshold.
        var result = await Handle(new RequestLeaveCommand(_bruno.Id, LeaveType.Vacation, TestData.Weeks.PlainStart, TestData.Weeks.PlainEnd, "Family trip"));

        Assert.False(result.IsError);
        Assert.NotEqual(default, result.Value);
        await _host.LeaveRequests.Received(1).AddAsync(
            Arg.Is<Domain.LeaveRequests.LeaveRequest>(request => request.Status == RequestStatus.Pending && request.Reason == "Family trip"),
            Arg.Any<CancellationToken>());
        await _host.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _host.Dispatcher.DidNotReceiveWithAnyArgs().DispatchAsync(default(IDomainEvent)!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RemainingBalanceBelowOneDay_DispatchesLowBalanceWarningAfterSave()
    {
        // A tiny 2h/month policy accrues 15.16h by frozen today; one Monday costs 8h,
        // leaving 7.16h — below the 8h threshold, so the warning event must be dispatched.
        var tinyPolicy = TestData.VacationMonthly(hoursPerPeriod: 2m, minTenureMonths: 0);
        var carla = TestData.Employee("Carla Gomez", "carla@leavelite.io", hiredOn: new DateOnly(2026, 1, 5), policyId: tinyPolicy.Id);
        _host.Employees.GetByIdAsync(carla.Id, Arg.Any<CancellationToken>()).Returns(carla);
        _host.Policies.GetByIdAsync(tinyPolicy.Id, Arg.Any<CancellationToken>()).Returns(tinyPolicy);
        _host.LeaveRequests.ListByEmployeeAsync(carla.Id, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Domain.LeaveRequests.LeaveRequest>());

        var result = await Handle(new RequestLeaveCommand(carla.Id, LeaveType.Vacation, TestData.Weeks.NextMonday, TestData.Weeks.NextMonday));

        Assert.False(result.IsError);
        await _host.Dispatcher.Received(1).DispatchAsync(
            Arg.Is<LowBalanceWarningDomainEvent>(warning =>
                warning.EmployeeId == carla.Id
                && warning.LeaveType == LeaveType.Vacation
                && warning.BalanceHours == 7.16m
                && warning.ThresholdHours == 8m
                && warning.AsOf == ApplicationTestHost.Today),
            Arg.Any<CancellationToken>());

        // The request must be persisted before the warning is published.
        NSubstitute.Received.InOrder(async () =>
        {
            await _host.LeaveRequests.AddAsync(Arg.Any<Domain.LeaveRequests.LeaveRequest>(), Arg.Any<CancellationToken>());
            await _host.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            await _host.Dispatcher.DispatchAsync(Arg.Any<LowBalanceWarningDomainEvent>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task RequestedHoursExceedBalance_ReturnsInsufficientBalance()
    {
        // Tiny 2h/month policy accrues 15.16h by frozen today; Mon+Tue cost 16h — overdrawn.
        var tinyPolicy = TestData.VacationMonthly(hoursPerPeriod: 2m, minTenureMonths: 0);
        var carla = TestData.Employee("Carla Gomez", "carla@leavelite.io", hiredOn: new DateOnly(2026, 1, 5), policyId: tinyPolicy.Id);
        _host.Employees.GetByIdAsync(carla.Id, Arg.Any<CancellationToken>()).Returns(carla);
        _host.Policies.GetByIdAsync(tinyPolicy.Id, Arg.Any<CancellationToken>()).Returns(tinyPolicy);
        _host.LeaveRequests.ListByEmployeeAsync(carla.Id, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Domain.LeaveRequests.LeaveRequest>());

        var result = await Handle(new RequestLeaveCommand(carla.Id, LeaveType.Vacation, TestData.Weeks.NextMonday, TestData.Weeks.NextTuesday));

        Assert.True(result.IsError);
        Assert.Equal("LeaveRequest.InsufficientBalance", result.FirstError.Code);
        Assert.Contains("deficit", result.FirstError.Description);
        await _host.LeaveRequests.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
        await _host.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(TestContext.Current.CancellationToken);
        await _host.Dispatcher.DidNotReceiveWithAnyArgs().DispatchAsync(default(IDomainEvent)!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartDateInThePast_ReturnsStartDateInPast()
    {
        var result = await Handle(new RequestLeaveCommand(_bruno.Id, LeaveType.Vacation, ApplicationTestHost.Today.AddDays(-1), TestData.Weeks.PlainEnd));

        Assert.True(result.IsError);
        Assert.Equal("LeaveRequest.StartDateInPast", result.FirstError.Code);
        Assert.Contains("2026-08-21", result.FirstError.Description);
        await _host.LeaveRequests.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OverlappingPendingOrApprovedRequest_ReturnsOverlappingRequest()
    {
        var existing = TestData.Pending(_bruno.Id, TestData.Weeks.PlainStart, TestData.Weeks.PlainEnd);
        _host.LeaveRequests.GetOverlappingAsync(Arg.Any<EmployeeId>(), Arg.Any<Domain.ValueObjects.DateRange>(), Arg.Any<CancellationToken>())
            .Returns(new[] { existing });

        var result = await Handle(new RequestLeaveCommand(_bruno.Id, LeaveType.Vacation, TestData.Weeks.PlainEnd, TestData.Weeks.PlainEnd));

        Assert.True(result.IsError);
        Assert.Equal("LeaveRequest.OverlappingRequest", result.FirstError.Code);
        await _host.LeaveRequests.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OverlappingCancelledRequest_DoesNotBlockSubmission()
    {
        var cancelled = TestData.Pending(_bruno.Id, TestData.Weeks.PlainStart, TestData.Weeks.PlainEnd);
        cancelled.Cancel(ApplicationTestHost.Today);
        _host.LeaveRequests.GetOverlappingAsync(Arg.Any<EmployeeId>(), Arg.Any<Domain.ValueObjects.DateRange>(), Arg.Any<CancellationToken>())
            .Returns(new[] { cancelled });

        var result = await Handle(new RequestLeaveCommand(_bruno.Id, LeaveType.Vacation, TestData.Weeks.PlainStart, TestData.Weeks.PlainEnd));

        Assert.False(result.IsError);
        await _host.LeaveRequests.Received(1).AddAsync(Arg.Any<Domain.LeaveRequests.LeaveRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EndBeforeStart_FailsValidationBeforeDateRangeConstruction()
    {
        // The FluentValidation rule (not DateRange.Create) is the first gate: the error code is
        // the property name "End", not DateRange.StartAfterEnd.
        var result = await Handle(new RequestLeaveCommand(_bruno.Id, LeaveType.Vacation, TestData.Weeks.PlainEnd, TestData.Weeks.PlainStart));

        Assert.True(result.IsError);
        Assert.Equal("End", result.FirstError.Code);
        Assert.Contains("on or after", result.FirstError.Description);
        await _host.LeaveRequests.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnknownEmployee_ReturnsNotFound()
    {
        var result = await Handle(new RequestLeaveCommand(EmployeeId.New(), LeaveType.Vacation, TestData.Weeks.PlainStart, TestData.Weeks.PlainEnd));

        Assert.True(result.IsError);
        Assert.Equal("Employee.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task LeaveTypeMismatchWithEnrolledPolicy_ReturnsNoPolicyForLeaveType()
    {
        var result = await Handle(new RequestLeaveCommand(_bruno.Id, LeaveType.Parental, TestData.Weeks.PlainStart, TestData.Weeks.PlainEnd));

        Assert.True(result.IsError);
        Assert.Equal("Employee.NoPolicyForLeaveType", result.FirstError.Code);
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
