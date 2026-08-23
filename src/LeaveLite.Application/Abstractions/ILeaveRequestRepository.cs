using LeaveLite.Domain.Enums;
using LeaveLite.Domain.LeaveRequests;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Application.Abstractions;

/// <summary>Persistence abstraction for <see cref="LeaveRequest"/> aggregates.</summary>
public interface ILeaveRequestRepository
{
    Task<LeaveRequest?> GetByIdAsync(LeaveRequestId id, CancellationToken cancellationToken = default);

    Task AddAsync(LeaveRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveRequest>> ListByEmployeeAsync(EmployeeId employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests of the employees in <paramref name="teamId"/>, optionally filtered by status and
    /// optionally restricted to those overlapping [from, to]. Full lists, no paging.
    /// </summary>
    Task<IReadOnlyList<LeaveRequest>> ListByTeamAsync(
        Guid teamId,
        RequestStatus? status = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the employee's requests that overlap the range, regardless of status — callers filter.</summary>
    Task<IReadOnlyList<LeaveRequest>> GetOverlappingAsync(EmployeeId employeeId, DateRange range, CancellationToken cancellationToken = default);
}
