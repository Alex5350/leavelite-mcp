using LeaveLite.Application.Abstractions;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.LeaveRequests;
using LeaveLite.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LeaveLite.Infrastructure.Persistence.Repositories;

internal sealed class LeaveRequestRepository(LeaveLiteDbContext context) : ILeaveRequestRepository
{
    public Task<LeaveRequest?> GetByIdAsync(LeaveRequestId id, CancellationToken cancellationToken = default)
        => context.LeaveRequests.FirstOrDefaultAsync(request => request.Id == id, cancellationToken);

    public async Task AddAsync(LeaveRequest request, CancellationToken cancellationToken = default)
        => await context.LeaveRequests.AddAsync(request, cancellationToken);

    public async Task<IReadOnlyList<LeaveRequest>> ListByEmployeeAsync(EmployeeId employeeId, CancellationToken cancellationToken = default)
        => await context.LeaveRequests
            .Where(request => request.EmployeeId == employeeId)
            .OrderBy(request => request.DateRange.Start)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LeaveRequest>> ListByTeamAsync(
        Guid teamId,
        RequestStatus? status = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.LeaveRequests
            .Where(request => context.Employees.Any(employee => employee.Id == request.EmployeeId && employee.TeamId == teamId));

        if (status is { } filterStatus)
        {
            query = query.Where(request => request.Status == filterStatus);
        }

        // Overlap semantics: the request's range intersects [from, to], each bound optional.
        if (from is { } fromBound)
        {
            query = query.Where(request => request.DateRange.End >= fromBound);
        }

        if (to is { } toBound)
        {
            query = query.Where(request => request.DateRange.Start <= toBound);
        }

        return await query
            .OrderBy(request => request.DateRange.Start)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LeaveRequest>> GetOverlappingAsync(EmployeeId employeeId, DateRange range, CancellationToken cancellationToken = default)
        => await context.LeaveRequests
            .Where(request => request.EmployeeId == employeeId
                && request.DateRange.Start <= range.End
                && request.DateRange.End >= range.Start)
            .ToListAsync(cancellationToken);
}
