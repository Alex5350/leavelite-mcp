using System.Diagnostics.CodeAnalysis;
using ErrorOr;
using LeaveLite.Domain.Errors;

namespace LeaveLite.Domain.ValueObjects;

/// <summary>
/// An inclusive date range. Guarantees <see cref="Start"/> is on or before <see cref="End"/>.
/// </summary>
public sealed record DateRange
{
    private DateRange(DateOnly start, DateOnly end)
    {
        Start = start;
        End = end;
    }

    public DateOnly Start { get; }

    public DateOnly End { get; }

    /// <summary>Total number of calendar days in the range, boundaries included (no exclusions at this level).</summary>
    public int NumberOfDays => End.DayNumber - Start.DayNumber + 1;

    public static bool TryCreate(DateOnly start, DateOnly end, [NotNullWhen(true)] out DateRange? dateRange)
    {
        if (start > end)
        {
            dateRange = null;
            return false;
        }

        dateRange = new DateRange(start, end);
        return true;
    }

    /// <summary>Creates a validated range or returns <see cref="DateRangeErrors.StartAfterEnd"/>.</summary>
    public static ErrorOr<DateRange> Create(DateOnly start, DateOnly end)
        => TryCreate(start, end, out var dateRange)
            ? dateRange
            : DateRangeErrors.StartAfterEnd(start, end);

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    /// <summary>True when the ranges share at least one calendar day (inclusive boundaries).</summary>
    public bool Overlaps(DateRange other) => Start <= other.End && other.Start <= End;

    public bool DisjointWith(DateRange other) => !Overlaps(other);

    public override string ToString() => $"{Start:O}..{End:O}";
}
