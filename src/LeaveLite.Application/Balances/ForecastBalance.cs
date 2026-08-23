using ErrorOr;
using FluentValidation;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Common;
using LeaveLite.Domain.Balances;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Errors;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Application.Balances;

/// <summary>Current balance plus the projected balance at the horizon date.</summary>
public sealed record ForecastBalanceDto(
    int MonthsAhead,
    DateOnly Horizon,
    BalanceDto Current,
    BalanceDto Projected);

/// <summary>
/// Projects the balance <paramref name="MonthsAhead"/> months from today by running the same
/// accrual engine with asOf = horizon. Approved future leave is already counted in Consumed.
/// </summary>
public sealed record ForecastBalanceQuery(EmployeeId EmployeeId, LeaveType LeaveType, int MonthsAhead)
    : IQuery<ForecastBalanceDto>;

public sealed class ForecastBalanceValidator : AbstractValidator<ForecastBalanceQuery>
{
    public ForecastBalanceValidator()
    {
        RuleFor(query => query.EmployeeId).NotEqual(default(EmployeeId));
        RuleFor(query => query.LeaveType).IsInEnum();
        RuleFor(query => query.MonthsAhead).InclusiveBetween(1, 12);
    }
}

internal sealed class ForecastBalanceHandler(
    IEmployeeRepository employees,
    ILeaveRequestRepository leaveRequests,
    IAccrualPolicyRepository policies,
    IHolidayCalendarRepository holidayCalendars,
    IBalanceCalculator calculator,
    IDateTimeProvider time,
    IValidator<ForecastBalanceQuery> validator) : IQueryHandler<ForecastBalanceQuery, ForecastBalanceDto>
{
    public async Task<ErrorOr<ForecastBalanceDto>> Handle(ForecastBalanceQuery query, CancellationToken cancellationToken)
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

        var today = time.Today;
        var horizon = today.AddMonths(query.MonthsAhead);

        var history = await leaveRequests.ListByEmployeeAsync(employee.Id, cancellationToken);
        var holidays = await HolidaySupport.LoadAsync(
            holidayCalendars,
            HolidaySupport.CollectCoveredYears(history, today, horizon),
            cancellationToken);

        var current = calculator.Calculate(employee, policy, today, history, holidays);
        var projected = calculator.Calculate(employee, policy, horizon, history, holidays);

        return new ForecastBalanceDto(
            query.MonthsAhead,
            horizon,
            BalanceDto.From(current),
            BalanceDto.From(projected));
    }
}
