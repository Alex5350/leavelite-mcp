using System.Threading.Channels;
using LeaveLite.Application.Abstractions;
using LeaveLite.Domain.Balances;
using LeaveLite.Domain.Common;
using Microsoft.Extensions.Logging;

namespace LeaveLite.Infrastructure.Messaging;

/// <summary>Configuration of the bounded low-balance alert pipeline.</summary>
public static class LowBalanceAlertChannel
{
    public const int Capacity = 64;

    /// <summary>Alert bursts are expendable: when the buffer is full, newest warnings are dropped.</summary>
    public static BoundedChannelOptions Options { get; } = new(Capacity)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
    };
}

/// <summary>
/// Every dispatched domain event is logged; low-balance warnings are additionally published into
/// a bounded channel drained by <see cref="LowBalanceAlertWorker"/> — an in-process stand-in for a
/// real alerting pipeline (broker + consumer) with the same backpressure semantics.
/// </summary>
internal sealed class ChannelDomainEventDispatcher(
    ILogger<ChannelDomainEventDispatcher> logger,
    ChannelWriter<LowBalanceWarningDomainEvent> lowBalanceAlerts) : IDomainEventDispatcher
{
    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        => DispatchAsync([domainEvent], cancellationToken);

    public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            logger.LogInformation(
                "Domain event {EventType}: {DomainEvent}",
                domainEvent.GetType().Name,
                domainEvent);

            if (domainEvent is LowBalanceWarningDomainEvent warning)
            {
                // TryWrite never blocks on a bounded DropWrite channel; false means the warning was dropped.
                if (!lowBalanceAlerts.TryWrite(warning))
                {
                    logger.LogWarning(
                        "Low-balance alert channel is full (capacity {Capacity}); dropped warning for employee {EmployeeId}.",
                        LowBalanceAlertChannel.Capacity,
                        warning.EmployeeId);
                }
            }
        }

        return Task.CompletedTask;
    }
}
