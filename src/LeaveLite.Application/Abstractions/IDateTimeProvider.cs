namespace LeaveLite.Application.Abstractions;

/// <summary>
/// The only way application code learns "what time is it". Implementations are provided
/// by the host/infrastructure layer (e.g. system clock vs. frozen test clock).
/// </summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }

    /// <summary>The current date in the host's operating timezone.</summary>
    DateOnly Today { get; }
}
