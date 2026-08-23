using ErrorOr;
using FluentValidation;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Common;

namespace LeaveLite.Application.Calendars;

/// <summary>A named holiday date.</summary>
public sealed record HolidayDto(DateOnly Date, string Name);

/// <summary>All configured holidays for a year (empty list when none are configured).</summary>
public sealed record ListHolidaysQuery(int Year) : IQuery<IReadOnlyList<HolidayDto>>;

public sealed class ListHolidaysValidator : AbstractValidator<ListHolidaysQuery>
{
    public ListHolidaysValidator()
    {
        RuleFor(query => query.Year).InclusiveBetween(2000, 2200);
    }
}

internal sealed class ListHolidaysHandler(
    IHolidayCalendarRepository holidayCalendars,
    IValidator<ListHolidaysQuery> validator) : IQueryHandler<ListHolidaysQuery, IReadOnlyList<HolidayDto>>
{
    public async Task<ErrorOr<IReadOnlyList<HolidayDto>>> Handle(ListHolidaysQuery query, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorsAsync(query, cancellationToken) is { } validationErrors)
        {
            return validationErrors;
        }

        if (await holidayCalendars.GetAsync(query.Year, cancellationToken) is not { } calendar)
        {
            return Array.Empty<HolidayDto>();
        }

        return calendar.Holidays
            .OrderBy(static holiday => holiday.Date)
            .Select(static holiday => new HolidayDto(holiday.Date, holiday.Name))
            .ToList();
    }
}
