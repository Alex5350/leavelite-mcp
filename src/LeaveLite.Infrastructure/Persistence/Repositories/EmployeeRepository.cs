using LeaveLite.Application.Abstractions;
using LeaveLite.Domain.Employees;
using LeaveLite.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LeaveLite.Infrastructure.Persistence.Repositories;

internal sealed class EmployeeRepository(LeaveLiteDbContext context) : IEmployeeRepository
{
    public Task<Employee?> GetByIdAsync(EmployeeId id, CancellationToken cancellationToken = default)
        => context.Employees.FirstOrDefaultAsync(employee => employee.Id == id, cancellationToken);

    public Task<Employee?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
        => context.Employees.FirstOrDefaultAsync(employee => employee.Email == email, cancellationToken);

    public async Task<IReadOnlyList<Employee>> ListByTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
        => await context.Employees
            .Where(employee => employee.TeamId == teamId)
            .OrderBy(employee => employee.FullName)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Employee employee, CancellationToken cancellationToken = default)
        => await context.Employees.AddAsync(employee, cancellationToken);
}
