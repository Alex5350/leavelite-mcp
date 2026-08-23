using ErrorOr;

namespace LeaveLite.Domain.Errors;

public static class DateRangeErrors
{
    public static Error StartAfterEnd(DateOnly start, DateOnly end)
        => Error.Validation("DateRange.StartAfterEnd", $"Range start {start:O} is after end {end:O}.");
}
