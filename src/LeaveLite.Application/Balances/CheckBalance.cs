using ErrorOr;
using FluentValidation;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Common;
using LeaveLite.Domain.Balances;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Errors;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Application.Balances;

/// <summary>Balance snapshot for one employee's leave type: accrued, consumed, and remaining hours.</summary>
public sealed record BalanceDto(
    EmployeeId EmployeeId,
    LeaveType LeaveType,
    DateOnly AsOf,
    decimal AccruedHours,
    decimal ConsumedHours,
    decimal BalanceHours)
{
    public static BalanceDto From(Balance balance) => new(
        balance.EmployeeId,
        balance.LeaveType,
        balance.AsOf,
        balance.AccruedHours,
        balance.ConsumedHours,
        balance.BalanceHours);
}

/// <summary>
/// Computes the leave balance as of today (or an explicit <see cref="AsOf"/> override) for the
/// single accrual policy the employee is enrolled in. The leave type must match that policy.
/// </summary>
public sealed record CheckBalanceQuery(EmployeeId EmployeeId, LeaveType LeaveType, DateOnly? AsOf = null)
    : IQuery<BalanceDto>;

public sealed class CheckBalanceValidator : AbstractValidator<CheckBalanceQuery>
{
    public CheckBalanceValidator()
    {
        RuleFor(query => query.EmployeeId).NotEqual(default(EmployeeId));
        RuleFor(query => query.LeaveType).IsInEnum();
        RuleFor(query => query.AsOf).NotEmpty().When(query => query.AsOf.HasValue);
    }
}

internal sealed class CheckBalanceHandler(
    IEmployeeRepository employees,
    ILeaveRequestRepository leaveRequests,
    IAccrualPolicyRepository policies,
    IHolidayCalendarRepository holidayCalendars,
    IBalanceCalculator calculator,
    IDateTimeProvider time,
    IValidator<CheckBalanceQuery> validator) : IQueryHandler<CheckBalanceQuery, BalanceDto>
{
    public async Task<ErrorOr<BalanceDto>> Handle(CheckBalanceQuery query, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorsAsync(query, cancellationToken) is { } validationErrors)
        {
            return validationErrors;
        }

        if (await employees.GetByIdAsync(query.EmployeeId, cancellationToken) is not { } employee)
        {
            return EmployeeErrors.NotFound(query.EmployeeId);
        }

        if (await policies.GetByIdAsync(employee.AccrualPolicyId, cancellationToken) is not { } policy)
        {
            return AccrualPolicyErrors.NotFound(employee.AccrualPolicyId.Value);
        }

        if (policy.LeaveType != query.LeaveType)
        {
            return EmployeeErrors.NoPolicyForLeaveType(query.LeaveType);
        }

        var asOf = query.AsOf ?? time.Today;

        var history = await leaveRequests.ListByEmployeeAsync(employee.Id, cancellationToken);
        var holidays = await HolidaySupport.LoadAsync(
            holidayCalendars,
            HolidaySupport.CollectCoveredYears(history, asOf, asOf),
            cancellationToken);

        var balance = calculator.Calculate(employee, policy, asOf, history, holidays);
        return BalanceDto.From(balance);
    }
}
