namespace LeaveLite.Domain.Holidays;

/// <summary>A named public holiday on a specific date.</summary>
public sealed record Holiday(DateOnly Date, string Name);
