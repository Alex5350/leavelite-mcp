using LeaveLite.Application.Abstractions;
using LeaveLite.Domain.Holidays;
using Microsoft.EntityFrameworkCore;

namespace LeaveLite.Infrastructure.Persistence.Repositories;

internal sealed class HolidayCalendarRepository(LeaveLiteDbContext context) : IHolidayCalendarRepository
{
    public async Task<HolidayCalendar?> GetAsync(int year, CancellationToken cancellationToken = default)
    {
        var row = await context.HolidayCalendars
            .FirstOrDefaultAsync(calendar => calendar.Year == year, cancellationToken);

        return row?.ToDomain();
    }
}
