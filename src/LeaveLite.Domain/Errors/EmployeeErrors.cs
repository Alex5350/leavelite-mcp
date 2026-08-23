using ErrorOr;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.Errors;

public static class EmployeeErrors
{
    public static Error NotFound(EmployeeId employeeId)
        => Error.NotFound("Employee.NotFound", $"Employee '{employeeId}' was not found.");

    public static Error FullNameRequired
        => Error.Validation("Employee.FullNameRequired", "Full name must not be empty.");

    public static Error InvalidEmail
        => Error.Validation("Employee.InvalidEmail", "A valid email address is required.");

    public static Error InvalidTeamId
        => Error.Validation("Employee.InvalidTeamId", "A non-empty team id is required.");

    public static Error InvalidEmploymentType
        => Error.Validation("Employee.InvalidEmploymentType", "Employment type must be a defined value.");

    public static Error InvalidTeamRole
        => Error.Validation("Employee.InvalidTeamRole", "Team role must be a defined value.");

    public static Error HiredOnRequired
        => Error.Validation("Employee.HiredOnRequired", "Hire date must be a real date (non-default).");

    public static Error DuplicateEmail(string email)
        => Error.Conflict("Employee.DuplicateEmail", $"An employee with email '{email}' already exists.");

    /// <summary>The employee's enrolled accrual policy is for a different leave type.</summary>
    public static Error NoPolicyForLeaveType(Enums.LeaveType leaveType)
        => Error.NotFound(
            "Employee.NoPolicyForLeaveType",
            $"The employee is not enrolled in an accrual policy covering '{leaveType}' leave.");

    public static Error NotAManager(EmployeeId employeeId)
        => Error.Forbidden("Employee.NotAManager", $"Employee '{employeeId}' does not have the Manager role.");

    public static Error PolicyNotEligible(Enums.EmploymentType employmentType, string policyName)
        => Error.Conflict(
            "Employee.PolicyNotEligible",
            $"Accrual policy '{policyName}' is not eligible for employment type '{employmentType}'.");
}
