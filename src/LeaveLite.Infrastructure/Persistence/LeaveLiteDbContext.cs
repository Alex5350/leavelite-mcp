using LeaveLite.Domain.Employees;
using LeaveLite.Domain.LeaveRequests;
using LeaveLite.Domain.Policies;
using Microsoft.EntityFrameworkCore;

namespace LeaveLite.Infrastructure.Persistence;

/// <summary>
/// EF Core mapping for the LeaveLite domain. Aggregates are mapped directly (private constructors
/// bind by parameter name), typed ids and value objects use value converters, and domain events
/// are deliberately ignored — they are a run-time concern, never persisted state.
/// </summary>
public sealed class LeaveLiteDbContext(DbContextOptions<LeaveLiteDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<AccrualPolicy> AccrualPolicies => Set<AccrualPolicy>();

    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    public DbSet<HolidayCalendarRow> HolidayCalendars => Set<HolidayCalendarRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(LeaveLiteDbContext).Assembly);
}
