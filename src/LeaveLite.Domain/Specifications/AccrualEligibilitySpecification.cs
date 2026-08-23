using LeaveLite.Domain.Employees;
using LeaveLite.Domain.Policies;

namespace LeaveLite.Domain.Specifications;

/// <summary>The employee and policy pair evaluated at a point in time.</summary>
public sealed record AccrualEligibilityContext(Employee Employee, AccrualPolicy Policy, DateOnly AsOf);

/// <summary>
/// Satisfied when the employee's employment type matches the policy and the tenure gate
/// (<see cref="AccrualPolicy.MinTenureMonths"/> full months since hire) has been reached.
/// </summary>
public sealed class AccrualEligibilitySpecification : Specification<AccrualEligibilityContext>
{
    public override bool IsSatisfiedBy(AccrualEligibilityContext context)
        => context.Policy.IsEligibleEmployment(context.Employee.EmploymentType)
            && context.AsOf >= context.Employee.HiredOn.AddMonths(context.Policy.MinTenureMonths);
}
