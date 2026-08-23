namespace LeaveLite.Application.Common;

/// <summary>Defaults for rules that are policy at the application level.</summary>
public static class StaffingDefaults
{
    /// <summary>Minimum team members that must remain available when approving leave.</summary>
    public const int MinimumStaffOnDuty = 1;

    /// <summary>
    /// Remaining balance (hours) below which a low-balance warning event is raised after a
    /// successful leave request — defaults to one working day of leave.
    /// </summary>
    public const decimal LowBalanceThresholdHours = Domain.Common.WorkSchedule.StandardHoursPerDay;
}
