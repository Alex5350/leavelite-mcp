using ErrorOr;
using FluentValidation;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Common;
using LeaveLite.Domain.Employees;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Errors;
using LeaveLite.Domain.Policies;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Application.Employees;

/// <summary>
/// Enrolls a new employee. The contractor-must-have-eligible-policy rule is enforced here:
/// the employment type must match the target policy's <see cref="AccrualPolicy.EmploymentType"/>.
/// </summary>
public sealed record RegisterEmployeeCommand(
    string FullName,
    string Email,
    EmploymentType EmploymentType,
    Guid TeamId,
    TeamRole TeamRole,
    DateOnly HiredOn,
    AccrualPolicyId PolicyId) : ICommand<EmployeeId>;

public sealed class RegisterEmployeeValidator : AbstractValidator<RegisterEmployeeCommand>
{
    public RegisterEmployeeValidator(IDateTimeProvider time)
    {
        RuleFor(command => command.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.Email)
            .Must(email => Domain.ValueObjects.Email.TryCreate(email, out _))
            .WithMessage("A valid email address is required.");

        RuleFor(command => command.EmploymentType).IsInEnum();
        RuleFor(command => command.TeamRole).IsInEnum();

        RuleFor(command => command.TeamId).NotEqual(Guid.Empty);
        RuleFor(command => command.PolicyId).NotEqual(default(AccrualPolicyId));

        RuleFor(command => command.HiredOn)
            .NotEmpty()
            .LessThanOrEqualTo(_ => time.Today)
            .WithMessage("Hire date cannot be in the future.");
    }
}

internal sealed class RegisterEmployeeHandler(
    IEmployeeRepository employees,
    IAccrualPolicyRepository policies,
    IUnitOfWork unitOfWork,
    IValidator<RegisterEmployeeCommand> validator) : ICommandHandler<RegisterEmployeeCommand, EmployeeId>
{
    public async Task<ErrorOr<EmployeeId>> Handle(RegisterEmployeeCommand command, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorsAsync(command, cancellationToken) is { } validationErrors)
        {
            return validationErrors;
        }

        if (await policies.GetByIdAsync(command.PolicyId, cancellationToken) is not { } policy)
        {
            return AccrualPolicyErrors.NotFound(command.PolicyId.Value);
        }

        // Contractor eligibility (and every other employment-type match) is enforced here, not in the aggregate.
        if (!policy.IsEligibleEmployment(command.EmploymentType))
        {
            return EmployeeErrors.PolicyNotEligible(command.EmploymentType, policy.Name);
        }

        if (!Domain.ValueObjects.Email.TryCreate(command.Email, out var email))
        {
            return EmailErrors.Invalid(command.Email);
        }

        if (await employees.GetByEmailAsync(email, cancellationToken) is not null)
        {
            return EmployeeErrors.DuplicateEmail(email.Value);
        }

        var created = Employee.Create(
            command.FullName,
            email.Value,
            command.EmploymentType,
            command.TeamId,
            command.TeamRole,
            command.HiredOn,
            command.PolicyId);
        if (created.IsError)
        {
            return created.Errors;
        }

        var employee = created.Value;

        await employees.AddAsync(employee, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return employee.Id;
    }
}
