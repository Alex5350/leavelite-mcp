using LeaveLite.Application.Abstractions;

namespace LeaveLite.Infrastructure.Time;

/// <summary>
/// Production clock built on DateTimeOffset.UtcNow (never DateTime.Now, which loses the offset
/// and reads the local clock twice). "Today" converts to the host's local timezone explicitly.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly Today => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.Local).Date);
}
