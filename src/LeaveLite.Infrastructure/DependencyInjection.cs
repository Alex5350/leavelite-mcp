using System.Threading.Channels;
using LeaveLite.Application.Abstractions;
using LeaveLite.Domain.Balances;
using LeaveLite.Infrastructure.Messaging;
using LeaveLite.Infrastructure.Persistence;
using LeaveLite.Infrastructure.Persistence.Repositories;
using LeaveLite.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeaveLite.Infrastructure;

/// <summary>Infrastructure layer composition root — the single entry point the host calls.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // The connection string is resolved INSIDE the options lambda (i.e. at first DbContext use),
        // so host-level overrides — environment variables like ConnectionStrings__LeaveLite,
        // per-environment appsettings, in-memory test configuration — always win over this default.
        services.AddDbContext<LeaveLiteDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("LeaveLite")
                ?? LeaveLiteDbContextFactory.DefaultConnectionString;

            options.UseSqlite(connectionString);
        });

        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<ILeaveRequestRepository, LeaveRequestRepository>();
        services.AddScoped<IAccrualPolicyRepository, AccrualPolicyRepository>();
        services.AddScoped<IHolidayCalendarRepository, HolidayCalendarRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Bounded low-balance alert pipeline: dispatcher -> channel -> background worker.
        var lowBalanceChannel = Channel.CreateBounded<LowBalanceWarningDomainEvent>(LowBalanceAlertChannel.Options);
        services.AddSingleton(lowBalanceChannel);
        services.AddSingleton(lowBalanceChannel.Reader);
        services.AddSingleton(lowBalanceChannel.Writer);
        services.AddSingleton<IDomainEventDispatcher, ChannelDomainEventDispatcher>();
        services.AddHostedService<LowBalanceAlertWorker>();

        // Development-time migrate + seed, invoked explicitly by the host.
        services.AddScoped<Initialization.DatabaseInitializer>();

        return services;
    }
}
