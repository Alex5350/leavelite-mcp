using LeaveLite.Domain.Common;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Errors;
using LeaveLite.Domain.LeaveRequests;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.UnitTests.LeaveRequests;

public sealed class LeaveRequestTests
{
    private static readonly DateOnly Start = new(2026, 9, 7);
    private static readonly DateOnly End = new(2026, 9, 11);

    private static LeaveRequest PendingRequest(string? reason = "Trip") => TestRequests.Pending(EmployeeId.New(), Start, End, reason: reason);

    [Fact]
    public void Create_ValidInput_StartsPendingWithFreshId()
    {
        var employeeId = EmployeeId.New();

        var result = LeaveRequest.Create(employeeId, LeaveType.Vacation, DateRange.Create(Start, End).Value, "  Trip  ", TestRequests.SubmittedAt);

        Assert.False(result.IsError);
        var request = result.Value;
        Assert.Equal(RequestStatus.Pending, request.Status);
        Assert.Equal(employeeId, request.EmployeeId);
        Assert.Equal(LeaveType.Vacation, request.LeaveType);
        Assert.Equal(Start, request.DateRange.Start);
        Assert.Equal(End, request.DateRange.End);
        Assert.Equal("Trip", request.Reason);
        Assert.Equal(TestRequests.SubmittedAt, request.SubmittedAtUtc);
        Assert.NotEqual(default, request.Id);
        Assert.Null(request.DecidedBy);
        Assert.Null(request.DecidedAtUtc);
        Assert.Null(request.DenialReason);
    }

    [Fact]
    public void Create_NullReason_IsPreservedAsNull()
    {
        var result = LeaveRequest.Create(EmployeeId.New(), LeaveType.Sick, DateRange.Create(Start, End).Value, null, TestRequests.SubmittedAt);

        Assert.False(result.IsError);
        Assert.Null(result.Value.Reason);
    }

    [Fact]
    public void Create_DefaultEmployeeId_ReturnsInvalidEmployeeId()
    {
        var result = LeaveRequest.Create(default, LeaveType.Vacation, DateRange.Create(Start, End).Value, null, TestRequests.SubmittedAt);

        Assert.True(result.IsError);
        Assert.Equal(LeaveRequestErrors.InvalidEmployeeId.Code, result.FirstError.Code);
    }

    [Fact]
    public void Create_UndefinedLeaveType_ReturnsInvalidLeaveType()
    {
        var result = LeaveRequest.Create(EmployeeId.New(), (LeaveType)99, DateRange.Create(Start, End).Value, null, TestRequests.SubmittedAt);

        Assert.True(result.IsError);
        Assert.Equal(LeaveRequestErrors.InvalidLeaveType.Code, result.FirstError.Code);
    }

    [Fact]
    public void Create_ReasonOver2000Characters_ReturnsInvalidReasonLength()
    {
        var result = LeaveRequest.Create(
            EmployeeId.New(), LeaveType.Vacation, DateRange.Create(Start, End).Value,
            new string('x', 2001), TestRequests.SubmittedAt);

        Assert.True(result.IsError);
        Assert.Equal(LeaveRequestErrors.InvalidReasonLength.Code, result.FirstError.Code);
    }

    [Fact]
    public void Create_ReasonExactly2000Characters_IsAccepted()
    {
        var result = LeaveRequest.Create(
            EmployeeId.New(), LeaveType.Vacation, DateRange.Create(Start, End).Value,
            new string('x', 2000), TestRequests.SubmittedAt);

        Assert.False(result.IsError);
    }

    [Fact]
    public void Create_DefaultSubmittedAt_ReturnsValidationError()
    {
        var result = LeaveRequest.Create(EmployeeId.New(), LeaveType.Vacation, DateRange.Create(Start, End).Value, null, default);

        Assert.True(result.IsError);
        Assert.Equal("LeaveRequest.InvalidSubmittedAt", result.FirstError.Code);
    }

    [Fact]
    public void Approve_PendingRequest_ApprovesAndRecordsDecision()
    {
        var request = PendingRequest();
        var approver = EmployeeId.New();
        var decidedAt = TestRequests.DecidedAt;

        var result = request.Approve(approver, decidedAt);

        Assert.False(result.IsError);
        Assert.Equal(RequestStatus.Approved, request.Status);
        Assert.Equal(approver, request.DecidedBy);
        Assert.Equal(decidedAt, request.DecidedAtUtc);
        Assert.Null(request.DenialReason);
    }

    [Fact]
    public void Approve_DefaultApprover_ReturnsInvalidApprover()
    {
        var request = PendingRequest();

        var result = request.Approve(default, TestRequests.DecidedAt);

        Assert.True(result.IsError);
        Assert.Equal(LeaveRequestErrors.InvalidApprover.Code, result.FirstError.Code);
        Assert.Equal(RequestStatus.Pending, request.Status);
    }

    [Fact]
    public void Approve_AlreadyApprovedRequest_ReturnsAlreadyDecided()
    {
        var request = PendingRequest();
        request.Approve(EmployeeId.New(), TestRequests.DecidedAt);

        var result = request.Approve(EmployeeId.New(), TestRequests.DecidedAt);

        Assert.True(result.IsError);
        Assert.Equal(LeaveRequestErrors.AlreadyDecided(RequestStatus.Approved).Code, result.FirstError.Code);
    }

    [Fact]
    public void Approve_AfterDenial_ReturnsAlreadyDecided()
    {
        var request = PendingRequest();
        request.Deny(EmployeeId.New(), "Not now", TestRequests.DecidedAt);

        var result = request.Approve(EmployeeId.New(), TestRequests.DecidedAt);

        Assert.True(result.IsError);
        Assert.Equal(LeaveRequestErrors.AlreadyDecided(RequestStatus.Denied).Code, result.FirstError.Code);
        Assert.Equal(RequestStatus.Denied, request.Status);
    }

    [Fact]
    public void Deny_PendingRequestWithReason_DeniesAndTrimsReason()
    {
        var request = PendingRequest();
        var approver = EmployeeId.New();
        var decidedAt = TestRequests.DecidedAt;

        var result = request.Deny(approver, "  End of year freeze  ", decidedAt);

        Assert.False(result.IsError);
        Assert.Equal(RequestStatus.Denied, request.Status);
        Assert.Equal(approver, request.DecidedBy);
        Assert.Equal(decidedAt, request.DecidedAtUtc);
        Assert.Equal("End of year freeze", request.DenialReason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Deny_MissingReason_ReturnsDenialReasonRequired(string? reason)
    {
        var request = PendingRequest();

        var result = request.Deny(EmployeeId.New(), reason, TestRequests.DecidedAt);

        Assert.True(result.IsError);
        Assert.Equal(LeaveRequestErrors.DenialReasonRequired.Code, result.FirstError.Code);
        Assert.Equal(RequestStatus.Pending, request.Status);
    }

    [Fact]
    public void Deny_AlreadyDeniedRequest_ReturnsAlreadyDecided()
    {
        var request = PendingRequest();
        request.Deny(EmployeeId.New(), "Once is enough", TestRequests.DecidedAt);

        var result = request.Deny(EmployeeId.New(), "Second attempt", TestRequests.DecidedAt);

        Assert.True(result.IsError);
        Assert.Equal(LeaveRequestErrors.AlreadyDecided(RequestStatus.Denied).Code, result.FirstError.Code);
        Assert.Equal("Once is enough", request.DenialReason);
    }

    [Fact]
    public void Deny_AfterApproval_ReturnsAlreadyDecided()
    {
        var request = PendingRequest();
        request.Approve(EmployeeId.New(), TestRequests.DecidedAt);

        var result = request.Deny(EmployeeId.New(), "Changed my mind", TestRequests.DecidedAt);

        Assert.True(result.IsError);
        Assert.Equal(LeaveRequestErrors.AlreadyDecided(RequestStatus.Approved).Code, result.FirstError.Code);
        Assert.Equal(RequestStatus.Approved, request.Status);
    }

    [Fact]
    public void Cancel_PendingRequest_AlwaysSucceeds()
    {
        var request = PendingRequest();

        var result = request.Cancel(new DateOnly(2026, 12, 31)); // even far past the range

        Assert.False(result.IsError);
        Assert.Equal(RequestStatus.Cancelled, request.Status);
    }

    [Fact]
    public void Cancel_ApprovedRequestBeforeStart_Succeeds()
    {
        var request = PendingRequest();
        request.Approve(EmployeeId.New(), TestRequests.DecidedAt);

        var result = request.Cancel(Start.AddDays(-1));

        Assert.False(result.IsError);
        Assert.Equal(RequestStatus.Cancelled, request.Status);
    }

    [Fact]
    public void Cancel_ApprovedRequestOnItsStartDay_ReturnsCannotCancelStarted()
    {
        var request = PendingRequest();
        request.Approve(EmployeeId.New(), TestRequests.DecidedAt);

        var result = request.Cancel(Start);

        Assert.True(result.IsError);
        Assert.Equal(LeaveRequestErrors.CannotCancelStarted(Start).Code, result.FirstError.Code);
        Assert.Equal(RequestStatus.Approved, request.Status);
    }

    [Fact]
    public void Cancel_ApprovedRequestAfterStart_ReturnsCannotCancelStarted()
    {
        var request = PendingRequest();
        request.Approve(EmployeeId.New(), TestRequests.DecidedAt);

        var result = request.Cancel(End);

        Assert.True(result.IsError);
        Assert.Equal(LeaveRequestErrors.CannotCancelStarted(Start).Code, result.FirstError.Code);
        Assert.Equal(RequestStatus.Approved, request.Status);
    }

    [Fact]
    public void Cancel_DeniedRequest_ReturnsAlreadyDecided()
    {
        var request = PendingRequest();
        request.Deny(EmployeeId.New(), "No", TestRequests.DecidedAt);

        var result = request.Cancel(new DateOnly(2026, 8, 22));

        Assert.True(result.IsError);
        Assert.Equal(LeaveRequestErrors.AlreadyDecided(RequestStatus.Denied).Code, result.FirstError.Code);
    }

    [Fact]
    public void Cancel_CancelledRequest_ReturnsAlreadyDecided()
    {
        var request = PendingRequest();
        request.Cancel(new DateOnly(2026, 8, 22));

        var result = request.Cancel(new DateOnly(2026, 8, 22));

        Assert.True(result.IsError);
        Assert.Equal(LeaveRequestErrors.AlreadyDecided(RequestStatus.Cancelled).Code, result.FirstError.Code);
    }

    [Fact]
    public void Approve_RaisesApprovedEventExactlyOnce()
    {
        var request = PendingRequest();
        var approver = EmployeeId.New();

        request.Approve(approver, TestRequests.DecidedAt);

        var @event = Assert.Single(request.DomainEvents.OfType<LeaveRequestApprovedDomainEvent>());
        Assert.Equal(request.Id, @event.RequestId);
        Assert.Equal(request.EmployeeId, @event.EmployeeId);
        Assert.Equal(approver, @event.ApproverId);
        Assert.Equal(request.DateRange, @event.Period);
        Assert.Equal(TestRequests.DecidedAt, @event.DecidedAtUtc);
    }

    [Fact]
    public void Deny_RaisesDeniedEventWithTrimmedReason()
    {
        var request = PendingRequest();

        request.Deny(EmployeeId.New(), "  Staffing  ", TestRequests.DecidedAt);

        var @event = Assert.Single(request.DomainEvents.OfType<LeaveRequestDeniedDomainEvent>());
        Assert.Equal(request.Id, @event.RequestId);
        Assert.Equal("Staffing", @event.DenialReason);
    }

    [Fact]
    public void PullDomainEvents_ReturnsEventsOnceAndClearsTheCollection()
    {
        var request = PendingRequest();
        request.Approve(EmployeeId.New(), TestRequests.DecidedAt);

        var firstPull = request.PullDomainEvents();
        var secondPull = request.PullDomainEvents();

        Assert.Single(firstPull.OfType<LeaveRequestApprovedDomainEvent>());
        Assert.Empty(secondPull);
        Assert.Empty(request.DomainEvents);
    }

    [Fact]
    public void FailedTransition_RaisesNoDomainEvents()
    {
        var request = PendingRequest();
        request.Deny(EmployeeId.New(), null, TestRequests.DecidedAt); // fails: reason required

        Assert.Empty(request.DomainEvents);
        Assert.Equal(RequestStatus.Pending, request.Status);
    }

    [Fact]
    public void Cancel_RaisesNoDomainEvents()
    {
        var request = PendingRequest();

        request.Cancel(new DateOnly(2026, 8, 22));

        Assert.Empty(request.DomainEvents);
    }

    [Fact]
    public void EntityEquality_IsByIdentityNotByContent()
    {
        var employeeId = EmployeeId.New();
        var first = TestRequests.Pending(employeeId, Start, End);
        var second = TestRequests.Pending(employeeId, Start, End);

        Assert.NotEqual(first, second); // different ids
        Assert.Equal(first, first);
    }

    private sealed record DummyEvent : IDomainEvent;

    private sealed class DummyEntity(EmployeeId id) : Entity<EmployeeId>(id)
    {
        public void RaiseEvent(IDomainEvent domainEvent) => Raise(domainEvent);
    }

    [Fact]
    public void Entity_RaiseAndPull_ReturnsEachEventExactlyOnce()
    {
        var entity = new DummyEntity(EmployeeId.New());
        var first = new DummyEvent();
        var second = new DummyEvent();

        entity.RaiseEvent(first);
        entity.RaiseEvent(second);

        Assert.Equal(2, entity.DomainEvents.Count);
        var pulled = entity.PullDomainEvents();
        Assert.Equal(new IDomainEvent[] { first, second }, pulled);
        Assert.Empty(entity.DomainEvents);
    }
}
