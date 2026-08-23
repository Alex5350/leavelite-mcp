using LeaveLite.Domain.Policies;
using LeaveLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LeaveLite.Infrastructure.Persistence.Configurations;

internal sealed class AccrualPolicyConfiguration : IEntityTypeConfiguration<AccrualPolicy>
{
    public void Configure(EntityTypeBuilder<AccrualPolicy> builder)
    {
        builder.ToTable("AccrualPolicies");

        builder.HasKey(policy => policy.Id);

        builder.Property(policy => policy.Id)
            .HasConversion(PersistenceConverters.AccrualPolicyIdToGuid);

        builder.Ignore(policy => policy.DomainEvents);

        builder.Property(policy => policy.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(policy => policy.Name)
            .IsUnique();

        builder.Property(policy => policy.LeaveType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(policy => policy.EmploymentType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(policy => policy.AccrualPeriod)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(policy => policy.HoursPerPeriod)
            .HasPrecision(18, 2);

        builder.Property(policy => policy.AnnualCapHours)
            .HasPrecision(18, 2);

        builder.Property(policy => policy.CarryOverCapHours)
            .HasPrecision(18, 2);

        builder.Property(policy => policy.MinTenureMonths);
        builder.Property(policy => policy.GrantsBalanceUpfront);
    }
}
