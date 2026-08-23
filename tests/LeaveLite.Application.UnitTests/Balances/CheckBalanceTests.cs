using ErrorOr;
using NSubstitute;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Balances;
using LeaveLite.Application.Common;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Policies;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Application.UnitTests.Balances;

public sealed class CheckBalanceTests : IAsyncDisposable
{
    private readonly ApplicationTestHost _host = new();

    private readonly AccrualPolicy _vacationPolicy = TestData.VacationMonthly(); // 16h/month, 3-month gate

    private readonly Domain.Employees.Employee _bruno; // hired 2026-01-05, past the gate at frozen today

    public CheckBalanceTests()
    {
        _bruno = TestData.Employee("Bruno Chen", "bruno@leavelite.io", policyId: _vacationPolicy.Id);
        _host.Employees.GetByIdAsync(_bruno.Id, Arg.Any<CancellationToken>()).Returns(_bruno);
        _host.Policies.GetByIdAsync(_vacationPolicy.Id, Arg.Any<CancellationToken>()).Returns(_vacationPolicy);
        _host.LeaveRequests.ListByEmployeeAsync(_bruno.Id, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Domain.LeaveRequests.LeaveRequest>());
    }

    private Task<ErrorOr<BalanceDto>> Handle(CheckBalanceQuery query)
        => _host.Handler<IQueryHandler<CheckBalanceQuery, BalanceDto>>().Handle(query, TestContext.Current.CancellationToken);

    [Fact]
    public async Task HappyPath_ComputesAccrualAsOfFrozenToday()
    {
        // (7 whole months + 18 of the 31 days of the anchored period) * 16h = 121.29h.
        var result = await Handle(new CheckBalanceQuery(_bruno.Id, LeaveType.Vacation));

        Assert.False(result.IsError);
        var balance = result.Value;
        Assert.Equal(_bruno.Id, balance.EmployeeId);
        Assert.Equal(LeaveType.Vacation, balance.LeaveType);
        Assert.Equal(ApplicationTestHost.Today, balance.AsOf);
        Assert.Equal(121.29m, balance.AccruedHours);
        Assert.Equal(0m, balance.ConsumedHours);
        Assert.Equal(121.29m, balance.BalanceHours);
    }

    [Fact]
    public async Task AsOfOverride_ComputesAtTheRequestedDate()
    {
        var result = await Handle(new CheckBalanceQuery(_bruno.Id, LeaveType.Vacation, AsOf: new DateOnly(2026, 8, 31)));

        Assert.False(result.IsError);
        Assert.Equal(new DateOnly(2026, 8, 31), result.Value.AsOf);
        // (7 + 27/31) * 16 = 125.935... -> 125.94.
        Assert.Equal(125.94m, result.Value.AccruedHours);
    }

    [Fact]
    public async Task ApprovedLeave_SubtractsHolidayAdjustedWorkingHours()
    {
        var approved = TestData.Approved(_bruno.Id, TestData.Weeks.HolidayStart, TestData.Weeks.HolidayEnd);
        _host.LeaveRequests.ListByEmployeeAsync(_bruno.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { approved });
        _host.HolidayCalendars.GetAsync(2026, Arg.Any<CancellationToken>()).Returns(TestData.Us2026());

        var result = await Handle(new CheckBalanceQuery(_bruno.Id, LeaveType.Vacation));

        Assert.False(result.IsError);
        Assert.Equal(32m, result.Value.ConsumedHours); // Mon-Fri minus Labor Day
        Assert.Equal(121.29m - 32m, result.Value.BalanceHours);
    }

    [Fact]
    public async Task BeforeTenureGate_AccruesNothing()
    {
        var freshHirePolicy = TestData.VacationMonthly(); // 3-month gate
        var carla = TestData.Employee("Carla Gomez", "carla@leavelite.io", hiredOn: new DateOnly(2026, 7, 1), policyId: freshHirePolicy.Id);
        _host.Employees.GetByIdAsync(carla.Id, Arg.Any<CancellationToken>()).Returns(carla);
        _host.Policies.GetByIdAsync(freshHirePolicy.Id, Arg.Any<CancellationToken>()).Returns(freshHirePolicy);
        _host.LeaveRequests.ListByEmployeeAsync(carla.Id, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Domain.LeaveRequests.LeaveRequest>());

        var result = await Handle(new CheckBalanceQuery(carla.Id, LeaveType.Vacation));

        Assert.False(result.IsError);
        Assert.Equal(0m, result.Value.AccruedHours);
        Assert.Equal(0m, result.Value.BalanceHours);
    }

    [Fact]
    public async Task UnknownEmployee_ReturnsNotFound()
    {
        var result = await Handle(new CheckBalanceQuery(EmployeeId.New(), LeaveType.Vacation));

        Assert.True(result.IsError);
        Assert.Equal("Employee.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task MissingPolicy_ReturnsPolicyNotFound()
    {
        var orphan = TestData.Employee("Orphan Hire", "orphan@leavelite.io", policyId: AccrualPolicyId.New());
        _host.Employees.GetByIdAsync(orphan.Id, Arg.Any<CancellationToken>()).Returns(orphan);
        _host.Policies.GetByIdAsync(Arg.Any<AccrualPolicyId>(), Arg.Any<CancellationToken>()).Returns((AccrualPolicy?)null);

        var result = await Handle(new CheckBalanceQuery(orphan.Id, LeaveType.Vacation));

        Assert.True(result.IsError);
        Assert.Equal("AccrualPolicy.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task LeaveTypeMismatchWithEnrolledPolicy_ReturnsNoPolicyForLeaveType()
    {
        var result = await Handle(new CheckBalanceQuery(_bruno.Id, LeaveType.Sick));

        Assert.True(result.IsError);
        Assert.Equal("Employee.NoPolicyForLeaveType", result.FirstError.Code);
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
