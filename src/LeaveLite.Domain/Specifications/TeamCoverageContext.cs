using LeaveLite.Domain.Holidays;
using LeaveLite.Domain.LeaveRequests;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.Specifications;

/// <summary>
/// Everything the staffing rule needs to know about a team over a date range:
/// how many members exist (excluding the candidate whose request is being decided),
/// their approved leave, and the holidays to respect.
/// </summary>
public sealed record TeamCoverageContext(
    int TeamSize,
    DateRange Range,
    IReadOnlyCollection<LeaveRequest> ApprovedLeave,
    HolidayCalendar? Holidays = null);
