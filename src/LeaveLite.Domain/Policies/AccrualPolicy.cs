using ErrorOr;
using LeaveLite.Domain.Common;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Errors;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.Policies;

/// <summary>
/// Configuration entity describing how leave hours accrue for an employment type and leave type.
/// One policy covers exactly one <see cref="LeaveType"/>/<see cref="EmploymentType"/> combination.
/// </summary>
public sealed class AccrualPolicy : Entity<AccrualPolicyId>
{
    private AccrualPolicy(
        AccrualPolicyId id,
        string name,
        LeaveType leaveType,
        EmploymentType employmentType,
        AccrualPeriod accrualPeriod,
        decimal hoursPerPeriod,
        decimal? annualCapHours,
        decimal? carryOverCapHours,
        int minTenureMonths,
        bool grantsBalanceUpfront)
        : base(id)
    {
        Name = name;
        LeaveType = leaveType;
        EmploymentType = employmentType;
        AccrualPeriod = accrualPeriod;
        HoursPerPeriod = hoursPerPeriod;
        AnnualCapHours = annualCapHours;
        CarryOverCapHours = carryOverCapHours;
        MinTenureMonths = minTenureMonths;
        GrantsBalanceUpfront = grantsBalanceUpfront;
    }

    public string Name { get; }

    public LeaveType LeaveType { get; }

    /// <summary>The employment type eligible for this policy (e.g. a contractor-eligible Sick policy).</summary>
    public EmploymentType EmploymentType { get; }

    public AccrualPeriod AccrualPeriod { get; }

    public decimal HoursPerPeriod { get; }

    public decimal? AnnualCapHours { get; }

    public decimal? CarryOverCapHours { get; }

    public int MinTenureMonths { get; }

    /// <summary>
    /// When true (Parental/Sick-style), the full annual amount is granted once the employee
    /// passes the tenure gate instead of accruing period by period.
    /// </summary>
    public bool GrantsBalanceUpfront { get; }

    /// <summary>Number of accrual periods in a year: 12 for Monthly, 1 for Yearly.</summary>
    public int PeriodsPerYear => AccrualPeriod == AccrualPeriod.Monthly ? 12 : 1;

    /// <summary>The full annual entitlement: <see cref="HoursPerPeriod"/> x <see cref="PeriodsPerYear"/>.</summary>
    public decimal AnnualAmount => HoursPerPeriod * PeriodsPerYear;

    public bool IsEligibleEmployment(EmploymentType employmentType) => EmploymentType == employmentType;

    public static ErrorOr<AccrualPolicy> Create(
        string name,
        LeaveType leaveType,
        EmploymentType employmentType,
        AccrualPeriod accrualPeriod,
        decimal hoursPerPeriod,
        decimal? annualCapHours,
        decimal? carryOverCapHours,
        int minTenureMonths,
        bool grantsBalanceUpfront)
    {
        List<Error> errors = [];

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(AccrualPolicyErrors.NameRequired);
        }

        if (hoursPerPeriod <= 0)
        {
            errors.Add(AccrualPolicyErrors.HoursPerPeriodNotPositive(hoursPerPeriod));
        }

        // Invariant: caps must be at least the per-period accrual they cap.
        if (annualCapHours is { } annualCap && hoursPerPeriod > 0 && annualCap < hoursPerPeriod)
        {
            errors.Add(AccrualPolicyErrors.AnnualCapBelowAccrual(annualCap, hoursPerPeriod));
        }

        if (carryOverCapHours is { } carryOverCap && hoursPerPeriod > 0 && carryOverCap < hoursPerPeriod)
        {
            errors.Add(AccrualPolicyErrors.CarryOverCapBelowAccrual(carryOverCap, hoursPerPeriod));
        }

        if (minTenureMonths < 0)
        {
            errors.Add(AccrualPolicyErrors.MinTenureMonthsNegative);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        return new AccrualPolicy(
            AccrualPolicyId.New(),
            name.Trim(),
            leaveType,
            employmentType,
            accrualPeriod,
            hoursPerPeriod,
            annualCapHours,
            carryOverCapHours,
            minTenureMonths,
            grantsBalanceUpfront);
    }
}
