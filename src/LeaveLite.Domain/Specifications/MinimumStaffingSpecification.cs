namespace LeaveLite.Domain.Specifications;

/// <summary>
/// Satisfied when at least <see cref="MinimumStaff"/> team members are available on every
/// working day (Mon-Fri, holidays skipped) of the range. Availability means: not on approved
/// leave that day. A team smaller than the minimum can never satisfy the rule.
/// </summary>
public sealed class MinimumStaffingSpecification(int minimumStaff) : Specification<TeamCoverageContext>
{
    public int MinimumStaff { get; } = minimumStaff > 0 ? minimumStaff : 1;

    public override bool IsSatisfiedBy(TeamCoverageContext context)
    {
        if (context.TeamSize < MinimumStaff)
        {
            return false;
        }

        for (var date = context.Range.Start; date <= context.Range.End; date = date.AddDays(1))
        {
            if (!Common.WorkSchedule.IsWorkingDay(date, context.Holidays))
            {
                continue;
            }

            var onLeave = context.ApprovedLeave
                .Where(request => request.DateRange.Contains(date))
                .Select(request => request.EmployeeId)
                .Distinct()
                .Count();

            if (context.TeamSize - onLeave < MinimumStaff)
            {
                return false;
            }
        }

        return true;
    }
}
