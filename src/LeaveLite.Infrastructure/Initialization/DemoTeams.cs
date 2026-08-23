namespace LeaveLite.Infrastructure.Initialization;

/// <summary>
/// The demo organization's team directory. Teams are identity anchors (employees reference a
/// team Guid) rather than full aggregates, so the catalog lives here with FIXED, deterministic
/// ids — documented ids that never change between runs.
/// </summary>
public static class DemoTeams
{
    /// <summary>UUIDv5(NAMESPACE_DNS, "team.platform.leavelite.io") — stable id for the Platform team.</summary>
    public static readonly Guid PlatformId = new("e700e69a-9011-5c51-aa72-781b86f26323");

    public const string PlatformName = "Platform";

    public sealed record TeamInfo(Guid Id, string Name);

    public static readonly IReadOnlyList<TeamInfo> All = [new(PlatformId, PlatformName)];

    /// <summary>The team used when a caller does not specify one.</summary>
    public static TeamInfo Default => All[0];

    /// <summary>
    /// Case-insensitive lookup by exact team name. Returns false for null/empty/unknown names
    /// (Dictionary.TryGetValue-style); <paramref name="team"/> then holds <see cref="Default"/>.
    /// </summary>
    public static bool TryGetByName(string? name, out TeamInfo team)
    {
        team = All.FirstOrDefault(candidate => string.Equals(candidate.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? Default;

        return !string.IsNullOrWhiteSpace(name)
            && string.Equals(team.Name, name!.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>All team names, comma-separated — for error messages and help text.</summary>
    public static string Names()
        => string.Join(", ", All.Select(team => team.Name));
}
