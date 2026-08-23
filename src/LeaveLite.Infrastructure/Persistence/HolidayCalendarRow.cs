using System.Text.Json;
using LeaveLite.Domain.Holidays;

namespace LeaveLite.Infrastructure.Persistence;

/// <summary>
/// Persistence shape for <see cref="HolidayCalendar"/>. The domain object exposes a computed,
/// read-only holiday collection which EF cannot map, so entries are stored as a JSON document
/// (clean, schema-light, and perfectly suited to a per-year configuration blob on SQLite) and
/// converted to/from the domain type in the repository.
/// </summary>
public sealed class HolidayCalendarRow
{
    private static readonly JsonSerializerOptions SerializationOptions = new(JsonSerializerDefaults.Web);

    public Guid Id { get; set; }

    public int Year { get; set; }

    public string HolidaysJson { get; set; } = "[]";

    public static HolidayCalendarRow FromDomain(HolidayCalendar calendar)
        => new()
        {
            Id = calendar.Id,
            Year = calendar.Year,
            HolidaysJson = JsonSerializer.Serialize(calendar.Holidays, SerializationOptions),
        };

    /// <summary>
    /// Rebuilds the domain calendar. <see cref="HolidayCalendar"/> is a value-ish configuration
    /// object (identity metadata only), so a freshly minted instance id on read is harmless.
    /// </summary>
    public HolidayCalendar ToDomain()
    {
        var holidays = JsonSerializer.Deserialize<List<Holiday>>(HolidaysJson, SerializationOptions)
            ?? throw new InvalidOperationException($"Holiday calendar for {Year} contains invalid JSON.");
        return HolidayCalendar.Create(Year, holidays);
    }
}
