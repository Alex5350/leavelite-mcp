using LeaveLite.Domain.Employees;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Application.Abstractions;

/// <summary>Persistence abstraction for <see cref="Employee"/> aggregates.</summary>
public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(EmployeeId id, CancellationToken cancellationToken = default);

    Task<Employee?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Employee>> ListByTeamAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task AddAsync(Employee employee, CancellationToken cancellationToken = default);
}
