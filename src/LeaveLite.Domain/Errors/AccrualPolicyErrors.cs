using ErrorOr;

namespace LeaveLite.Domain.Errors;

public static class AccrualPolicyErrors
{
    public static Error NotFound(Guid policyId)
        => Error.NotFound("AccrualPolicy.NotFound", $"Accrual policy '{policyId}' was not found.");

    public static Error NameRequired
        => Error.Validation("AccrualPolicy.NameRequired", "Policy name must not be empty.");

    public static Error HoursPerPeriodNotPositive(decimal hoursPerPeriod)
        => Error.Validation(
            "AccrualPolicy.HoursPerPeriodNotPositive",
            $"Hours per period must be greater than zero (got {hoursPerPeriod}).");

    public static Error AnnualCapBelowAccrual(decimal annualCapHours, decimal hoursPerPeriod)
        => Error.Validation(
            "AccrualPolicy.AnnualCapBelowAccrual",
            $"Annual cap ({annualCapHours}h) must be at least the per-period accrual ({hoursPerPeriod}h).");

    public static Error CarryOverCapBelowAccrual(decimal carryOverCapHours, decimal hoursPerPeriod)
        => Error.Validation(
            "AccrualPolicy.CarryOverCapBelowAccrual",
            $"Carry-over cap ({carryOverCapHours}h) must be at least the per-period accrual ({hoursPerPeriod}h).");

    public static Error MinTenureMonthsNegative
        => Error.Validation("AccrualPolicy.MinTenureMonthsNegative", "Minimum tenure months must be zero or greater.");
}
