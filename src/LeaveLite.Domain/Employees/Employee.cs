using System.Diagnostics.CodeAnalysis;
using ErrorOr;
using LeaveLite.Domain.Common;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Errors;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.Employees;

/// <summary>
/// An employee enrolled in exactly one <see cref="Policies.AccrualPolicy"/>.
/// Note: the rule that a contractor must be enrolled in a contractor-eligible policy is
/// intentionally validated in the application layer (policy is passed to the factory there);
/// this aggregate only enforces its own local invariants.
/// </summary>
public sealed class Employee : Entity<EmployeeId>
{
    private Employee(
        EmployeeId id,
        string fullName,
        Email email,
        EmploymentType employmentType,
        Guid teamId,
        TeamRole teamRole,
        DateOnly hiredOn,
        AccrualPolicyId accrualPolicyId)
        : base(id)
    {
        FullName = fullName;
        Email = email;
        EmploymentType = employmentType;
        TeamId = teamId;
        TeamRole = teamRole;
        HiredOn = hiredOn;
        AccrualPolicyId = accrualPolicyId;
    }

    public string FullName { get; }

    public Email Email { get; }

    public EmploymentType EmploymentType { get; }

    public Guid TeamId { get; }

    public TeamRole TeamRole { get; }

    public DateOnly HiredOn { get; }

    public AccrualPolicyId AccrualPolicyId { get; }

    public static bool TryCreate(
        string fullName,
        string email,
        EmploymentType employmentType,
        Guid teamId,
        TeamRole teamRole,
        DateOnly hiredOn,
        AccrualPolicyId accrualPolicyId,
        [NotNullWhen(true)] out Employee? employee)
    {
        var result = Create(fullName, email, employmentType, teamId, teamRole, hiredOn, accrualPolicyId);
        employee = result.IsError ? null : result.Value;
        return !result.IsError;
    }

    /// <summary>Creates an employee with a fresh Guid v7 id, validating local invariants.</summary>
    public static ErrorOr<Employee> Create(
        string fullName,
        string email,
        EmploymentType employmentType,
        Guid teamId,
        TeamRole teamRole,
        DateOnly hiredOn,
        AccrualPolicyId accrualPolicyId)
    {
        if (!Email.TryCreate(email, out var validEmail))
        {
            return EmployeeErrors.InvalidEmail;
        }

        var errors = ValidateLocally(fullName, employmentType, teamId, teamRole, hiredOn, accrualPolicyId);
        if (errors.Count > 0)
        {
            return errors;
        }

        return new Employee(EmployeeId.New(), fullName.Trim(), validEmail, employmentType, teamId, teamRole, hiredOn, accrualPolicyId);
    }

    private static List<Error> ValidateLocally(
        string fullName,
        EmploymentType employmentType,
        Guid teamId,
        TeamRole teamRole,
        DateOnly hiredOn,
        AccrualPolicyId accrualPolicyId)
    {
        List<Error> errors = [];

        if (string.IsNullOrWhiteSpace(fullName))
        {
            errors.Add(EmployeeErrors.FullNameRequired);
        }

        if (teamId == Guid.Empty)
        {
            errors.Add(EmployeeErrors.InvalidTeamId);
        }

        if (!Enum.IsDefined(employmentType))
        {
            errors.Add(EmployeeErrors.InvalidEmploymentType);
        }

        if (!Enum.IsDefined(teamRole))
        {
            errors.Add(EmployeeErrors.InvalidTeamRole);
        }

        if (hiredOn == default)
        {
            errors.Add(EmployeeErrors.HiredOnRequired);
        }

        if (accrualPolicyId == default)
        {
            errors.Add(AccrualPolicyErrors.NotFound(Guid.Empty));
        }

        return errors;
    }
}
