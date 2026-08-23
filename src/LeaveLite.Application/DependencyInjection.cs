using FluentValidation;
using LeaveLite.Application.Common;
using LeaveLite.Domain.Balances;
using Microsoft.Extensions.DependencyInjection;

namespace LeaveLite.Application;

/// <summary>Application layer composition root — the single entry point the host calls.</summary>
public static class DependencyInjection
{
    private static readonly Type[] HandlerInterfaceDefinitions =
    [
        typeof(ICommandHandler<>),
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
    ];

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // The accrual engine is pure and stateless, so it is safe as a singleton.
        services.AddSingleton<IBalanceCalculator, AccrualBalanceCalculator>();

        RegisterCommandAndQueryHandlers(services);

        return services;
    }

    /// <summary>
    /// Scans this assembly for concrete handlers implementing ICommandHandler&lt;&gt; /
    /// ICommandHandler&lt;,&gt; / IQueryHandler&lt;,&gt; and registers them against those
    /// interfaces as scoped (they depend on scoped repositories and the unit of work).
    /// </summary>
    private static void RegisterCommandAndQueryHandlers(IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        var implementations = assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false });

        foreach (var implementation in implementations)
        {
            var handlerInterfaces = implementation
                .GetInterfaces()
                .Where(@interface => @interface.IsGenericType
                    && HandlerInterfaceDefinitions.Contains(@interface.GetGenericTypeDefinition()));

            foreach (var handlerInterface in handlerInterfaces)
            {
                services.AddScoped(handlerInterface, implementation);
            }
        }
    }
}
