using LeaveLite.Domain.Employees;
using LeaveLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LeaveLite.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");

        builder.HasKey(employee => employee.Id);

        builder.Property(employee => employee.Id)
            .HasConversion(PersistenceConverters.EmployeeIdToGuid);

        // Domain events are run-time messages, not persisted state.
        builder.Ignore(employee => employee.DomainEvents);

        builder.Property(employee => employee.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(employee => employee.Email)
            .HasConversion(PersistenceConverters.EmailToString)
            .HasMaxLength(254)
            .IsRequired();

        builder.HasIndex(employee => employee.Email)
            .IsUnique();

        // Enums as strings keep the SQLite demo database human-readable.
        builder.Property(employee => employee.EmploymentType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(employee => employee.TeamRole)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(employee => employee.AccrualPolicyId)
            .HasConversion(PersistenceConverters.AccrualPolicyIdToGuid);

        // Get-only properties are not discovered by convention; mapping them explicitly lets
        // EF bind them through the aggregate's constructor (immutable-entity pattern).
        builder.Property(employee => employee.TeamId);
        builder.Property(employee => employee.HiredOn);

        builder.HasIndex(employee => employee.TeamId);
    }
}
