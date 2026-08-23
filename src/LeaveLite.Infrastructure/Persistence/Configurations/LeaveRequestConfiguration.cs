using LeaveLite.Domain.LeaveRequests;
using LeaveLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LeaveLite.Infrastructure.Persistence.Configurations;

internal sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("LeaveRequests");

        builder.HasKey(request => request.Id);

        builder.Property(request => request.Id)
            .HasConversion(PersistenceConverters.LeaveRequestIdToGuid);

        builder.Ignore(request => request.DomainEvents);

        builder.Property(request => request.EmployeeId)
            .HasConversion(PersistenceConverters.EmployeeIdToGuid);

        builder.Property(request => request.LeaveType)
            .HasConversion<string>()
            .HasMaxLength(20);

        // The immutable DateRange value object maps as a complex type flattened into two columns.
        builder.ComplexProperty(
            request => request.DateRange,
            dateRange =>
            {
                dateRange.Property(range => range.Start)
                    .HasColumnName("StartDate");

                dateRange.Property(range => range.End)
                    .HasColumnName("EndDate");
            });

        builder.Property(request => request.Reason)
            .HasMaxLength(2000);

        builder.Property(request => request.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(request => request.SubmittedAtUtc);
        builder.Property(request => request.DecidedAtUtc);

        builder.Property(request => request.DecidedBy)
            .HasConversion(PersistenceConverters.EmployeeIdToGuid);

        builder.Property(request => request.DenialReason)
            .HasMaxLength(2000);

        builder.HasIndex(request => request.EmployeeId);

        builder.HasIndex(request => request.Status);
    }
}
