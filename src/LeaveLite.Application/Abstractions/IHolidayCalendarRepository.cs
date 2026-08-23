using LeaveLite.Domain.Holidays;

namespace LeaveLite.Application.Abstractions;

/// <summary>Persistence abstraction for per-year <see cref="HolidayCalendar"/> configuration.</summary>
public interface IHolidayCalendarRepository
{
    Task<HolidayCalendar?> GetAsync(int year, CancellationToken cancellationToken = default);
}
