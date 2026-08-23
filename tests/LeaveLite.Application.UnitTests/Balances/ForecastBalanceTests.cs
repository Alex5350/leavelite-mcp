using ErrorOr;
using NSubstitute;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Balances;
using LeaveLite.Application.Common;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Policies;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Application.UnitTests.Balances;

public sealed class ForecastBalanceTests : IAsyncDisposable
{
    private readonly ApplicationTestHost _host = new();

    private readonly AccrualPolicy _vacationPolicy = TestData.VacationMonthly();

    private readonly Domain.Employees.Employee _bruno;

    public ForecastBalanceTests()
    {
        _bruno = TestData.Employee("Bruno Chen", "bruno@leavelite.io", policyId: _vacationPolicy.Id);
        _host.Employees.GetByIdAsync(_bruno.Id, Arg.Any<CancellationToken>()).Returns(_bruno);
        _host.Policies.GetByIdAsync(_vacationPolicy.Id, Arg.Any<CancellationToken>()).Returns(_vacationPolicy);
        _host.LeaveRequests.ListByEmployeeAsync(_bruno.Id, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Domain.LeaveRequests.LeaveRequest>());
    }

    private Task<ErrorOr<ForecastBalanceDto>> Handle(ForecastBalanceQuery query)
        => _host.Handler<IQueryHandler<ForecastBalanceQuery, ForecastBalanceDto>>().Handle(query, TestContext.Current.CancellationToken);

    [Fact]
    public async Task HappyPath_ProjectsAccrualToTheHorizon()
    {
        // Today 2026-08-22 -> 121.29h; horizon 2026-11-22 (10 months + 18/30 of the current
        // period since Jan 5): (10 + 18/30) * 16 = 169.6h.
        var result = await Handle(new ForecastBalanceQuery(_bruno.Id, LeaveType.Vacation, MonthsAhead: 3));

        Assert.False(result.IsError);
        var forecast = result.Value;
        Assert.Equal(3, forecast.MonthsAhead);
        Assert.Equal(new DateOnly(2026, 11, 22), forecast.Horizon);
        Assert.Equal(ApplicationTestHost.Today, forecast.Current.AsOf);
        Assert.Equal(121.29m, forecast.Current.AccruedHours);
        Assert.Equal(new DateOnly(2026, 11, 22), forecast.Projected.AsOf);
        Assert.Equal(169.60m, forecast.Projected.AccruedHours);
        Assert.True(forecast.Projected.BalanceHours > forecast.Current.BalanceHours);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public async Task MonthsAheadOutsideBounds_ReturnsValidationError(int monthsAhead)
    {
        var result = await Handle(new ForecastBalanceQuery(_bruno.Id, LeaveType.Vacation, monthsAhead));

        Assert.True(result.IsError);
        Assert.Equal("MonthsAhead", result.FirstError.Code);
    }

    [Fact]
    public async Task UnknownEmployee_ReturnsNotFound()
    {
        var result = await Handle(new ForecastBalanceQuery(EmployeeId.New(), LeaveType.Vacation, 6));

        Assert.True(result.IsError);
        Assert.Equal("Employee.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task LeaveTypeMismatch_ReturnsNoPolicyForLeaveType()
    {
        var result = await Handle(new ForecastBalanceQuery(_bruno.Id, LeaveType.Sick, 6));

        Assert.True(result.IsError);
        Assert.Equal("Employee.NoPolicyForLeaveType", result.FirstError.Code);
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
