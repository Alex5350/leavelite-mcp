using LeaveLite.Application;
using LeaveLite.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LeaveLite.Application.UnitTests;

/// <summary>
/// Composition root for handler-level tests: registers the real application layer (real
/// validators, real pure AccrualBalanceCalculator) and substitutes every abstraction with
/// NSubstitute mocks on a frozen clock.
/// </summary>
internal sealed class ApplicationTestHost : IAsyncDisposable
{
    /// <summary>Saturday, 2026-08-22 — the frozen "today" for every handler test.</summary>
    public static readonly DateOnly Today = new(2026, 8, 22);

    public static readonly DateTimeOffset UtcNow = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);

    public IDateTimeProvider Time { get; } = Substitute.For<IDateTimeProvider>();

    public IEmployeeRepository Employees { get; } = Substitute.For<IEmployeeRepository>();

    public ILeaveRequestRepository LeaveRequests { get; } = Substitute.For<ILeaveRequestRepository>();

    public IAccrualPolicyRepository Policies { get; } = Substitute.For<IAccrualPolicyRepository>();

    public IHolidayCalendarRepository HolidayCalendars { get; } = Substitute.For<IHolidayCalendarRepository>();

    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public IDomainEventDispatcher Dispatcher { get; } = Substitute.For<IDomainEventDispatcher>();

    private readonly ServiceProvider _provider;

    public ApplicationTestHost()
    {
        Time.UtcNow.Returns(UtcNow);
        Time.Today.Returns(Today);

        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton(Time);
        services.AddSingleton(Employees);
        services.AddSingleton(LeaveRequests);
        services.AddSingleton(Policies);
        services.AddSingleton(HolidayCalendars);
        services.AddSingleton(UnitOfWork);
        services.AddSingleton(Dispatcher);
        _provider = services.BuildServiceProvider();
    }

    public T Handler<T>() where T : class => _provider.GetRequiredService<T>();

    public ValueTask DisposeAsync()
    {
        _provider.Dispose();
        return ValueTask.CompletedTask;
    }
}
