using LeaveLite.Domain.Holidays;
using LeaveLite.Domain.Specifications;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.UnitTests.Specifications;

public sealed class MinimumStaffingSpecificationTests
{
    private static readonly DateRange PlainWeek = DateRange.Create(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 18)).Value; // Mon-Fri
    private static readonly HolidayCalendar Us2026 = HolidayCalendar.Create(
        2026, [new Holiday(new DateOnly(2026, 9, 7), "Labor Day")]);

    private static TeamCoverageContext Context(
        int teamSize,
        DateRange range,
        IReadOnlyCollection<Domain.LeaveRequests.LeaveRequest>? approvedLeave = null,
        HolidayCalendar? holidays = null)
        => new(teamSize, range, approvedLeave ?? [], holidays);

    [Fact]
    public void TeamSmallerThanMinimum_IsNeverSatisfied()
    {
        var specification = new MinimumStaffingSpecification(3);

        Assert.False(specification.IsSatisfiedBy(Context(2, PlainWeek)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void NonPositiveMinimum_ClampsToOne(int minimum)
    {
        var specification = new MinimumStaffingSpecification(minimum);

        Assert.Equal(1, specification.MinimumStaff);
        // A two-person team with nobody away satisfies the clamped minimum.
        Assert.True(specification.IsSatisfiedBy(Context(2, PlainWeek)));
    }

    [Fact]
    public void TeamWithDisjointApprovedLeave_IsSatisfied()
    {
        var specification = new MinimumStaffingSpecification(1);
        var onLeave = TestRequests.Approved(EmployeeId.New(), new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 9));

        Assert.True(specification.IsSatisfiedBy(Context(2, PlainWeek, [onLeave])));
    }

    [Fact]
    public void EveryoneOnLeaveOnSomeWorkingDay_IsNotSatisfied()
    {
        var specification = new MinimumStaffingSpecification(1);
        var onLeave = TestRequests.Approved(EmployeeId.New(), PlainWeek.Start, PlainWeek.End);

        Assert.False(specification.IsSatisfiedBy(Context(1, PlainWeek, [onLeave])));
    }

    [Fact]
    public void LeaveCoveringOnlyPartOfTheRange_StillFailsTheCoveredDay()
    {
        var specification = new MinimumStaffingSpecification(2);
        var wednesdayOnly = TestRequests.Approved(EmployeeId.New(), new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 16));

        var result = specification.IsSatisfiedBy(Context(2, PlainWeek, [wednesdayOnly]));

        Assert.False(result); // Wed: 2 - 1 = 1 < 2
    }

    [Fact]
    public void LeaveTouchingOnlyRangeBoundaries_FailsThoseDays()
    {
        var specification = new MinimumStaffingSpecification(2);
        var edges = TestRequests.Approved(EmployeeId.New(), new DateOnly(2026, 9, 18), new DateOnly(2026, 9, 20)); // Fri + weekend

        Assert.False(specification.IsSatisfiedBy(Context(2, PlainWeek, [edges])));
    }

    [Fact]
    public void OverlappingRequestsByTheSameEmployee_CountAsOnePerson()
    {
        var specification = new MinimumStaffingSpecification(2);
        var employeeId = EmployeeId.New();
        var first = TestRequests.Approved(employeeId, PlainWeek.Start, PlainWeek.End);
        var second = TestRequests.Approved(employeeId, new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 17));

        var result = specification.IsSatisfiedBy(Context(3, PlainWeek, [first, second]));

        Assert.True(result); // distinct people: 3 - 1 = 2 >= 2
    }

    [Fact]
    public void WeekendOnlyAbsence_DoesNotReduceWorkingDayCoverage()
    {
        var specification = new MinimumStaffingSpecification(1);
        var saturdaySunday = TestRequests.Approved(EmployeeId.New(), new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13));
        var rangeIncludingWeekend = DateRange.Create(new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 18)).Value;

        var result = specification.IsSatisfiedBy(Context(1, rangeIncludingWeekend, [saturdaySunday]));

        Assert.True(result); // weekend days are skipped entirely by the coverage loop
    }

    [Fact]
    public void HolidayOnlyAbsence_DoesNotCountAgainstCoverage()
    {
        var specification = new MinimumStaffingSpecification(1);
        // Sep 5 (Sat), Sep 6 (Sun), Sep 7 (Mon = Labor Day): no working days in the range at all.
        var range = DateRange.Create(new DateOnly(2026, 9, 5), new DateOnly(2026, 9, 7)).Value;
        var onLeave = TestRequests.Approved(EmployeeId.New(), range.Start, range.End);

        var result = specification.IsSatisfiedBy(Context(1, range, [onLeave], Us2026));

        Assert.True(result);
    }

    [Fact]
    public void HolidayInsideRange_IsSkippedByTheCoverageLoop()
    {
        var specification = new MinimumStaffingSpecification(1);
        // Mon Sep 7 is Labor Day: the sole teammate can be on leave that day without failing.
        var laborDayOnly = TestRequests.Approved(EmployeeId.New(), new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 7));
        var week = DateRange.Create(new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 11)).Value;

        var result = specification.IsSatisfiedBy(Context(1, week, [laborDayOnly], Us2026));

        Assert.True(result);
    }

    [Fact]
    public void SameDayLeaveByDifferentPeople_AggregatesHeadcount()
    {
        var specification = new MinimumStaffingSpecification(2);
        var first = TestRequests.Approved(EmployeeId.New(), new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 16));
        var second = TestRequests.Approved(EmployeeId.New(), new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 16));

        var result = specification.IsSatisfiedBy(Context(3, PlainWeek, [first, second]));

        Assert.False(result); // Wed: 3 - 2 = 1 < 2
    }
}
