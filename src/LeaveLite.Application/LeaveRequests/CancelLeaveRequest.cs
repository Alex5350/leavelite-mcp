using ErrorOr;
using FluentValidation;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Common;
using LeaveLite.Domain.Errors;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Application.LeaveRequests;

/// <summary>
/// Cancels a request. Only the owner may cancel; Pending always cancels, Approved only before
/// the leave starts (decided via <see cref="Domain.LeaveRequests.LeaveRequest.Cancel"/> with the
/// provider's Today).
/// </summary>
public sealed record CancelLeaveRequestCommand(LeaveRequestId RequestId, EmployeeId RequesterId) : ICommand;

public sealed class CancelLeaveRequestValidator : AbstractValidator<CancelLeaveRequestCommand>
{
    public CancelLeaveRequestValidator()
    {
        RuleFor(command => command.RequestId).NotEqual(default(LeaveRequestId));
        RuleFor(command => command.RequesterId).NotEqual(default(EmployeeId));
    }
}

internal sealed class CancelLeaveRequestHandler(
    ILeaveRequestRepository leaveRequests,
    IDateTimeProvider time,
    IUnitOfWork unitOfWork,
    IDomainEventDispatcher dispatcher,
    IValidator<CancelLeaveRequestCommand> validator) : ICommandHandler<CancelLeaveRequestCommand>
{
    public async Task<ErrorOr<Success>> Handle(CancelLeaveRequestCommand command, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorsAsync(command, cancellationToken) is { } validationErrors)
        {
            return validationErrors;
        }

        if (await leaveRequests.GetByIdAsync(command.RequestId, cancellationToken) is not { } request)
        {
            return LeaveRequestErrors.NotFound(command.RequestId);
        }

        if (request.EmployeeId != command.RequesterId)
        {
            return LeaveRequestErrors.NotOwner;
        }

        var cancelled = request.Cancel(time.Today);
        if (cancelled.IsError)
        {
            return cancelled.Errors;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await dispatcher.DispatchAsync(request.PullDomainEvents(), cancellationToken);

        return Result.Success;
    }
}
