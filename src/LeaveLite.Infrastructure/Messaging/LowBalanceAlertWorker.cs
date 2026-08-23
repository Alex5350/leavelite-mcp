using System.Threading.Channels;
using LeaveLite.Domain.Balances;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LeaveLite.Infrastructure.Messaging;

/// <summary>
/// Background consumer of the low-balance warning channel. Emits a structured warning log entry
/// per event — the same shape a real alerting sink (PagerDuty, Slack webhook, broker topic) would
/// receive — without dragging an external broker into the demo.
/// </summary>
internal sealed class LowBalanceAlertWorker(
    ChannelReader<LowBalanceWarningDomainEvent> lowBalanceAlerts,
    ILogger<LowBalanceAlertWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var warning in lowBalanceAlerts.ReadAllAsync(stoppingToken))
        {
            logger.LogWarning(
                "LOW BALANCE ALERT: employee {EmployeeId} has {BalanceHours}h of {LeaveType} left as of {AsOf} " +
                "(threshold {ThresholdHours}h, shortfall {ShortfallHours}h)",
                warning.EmployeeId,
                warning.BalanceHours,
                warning.LeaveType,
                warning.AsOf,
                warning.ThresholdHours,
                warning.ThresholdHours - warning.BalanceHours);
        }
    }
}
