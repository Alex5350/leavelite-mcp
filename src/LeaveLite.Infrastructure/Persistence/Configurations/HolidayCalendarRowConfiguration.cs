using LeaveLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LeaveLite.Infrastructure.Persistence.Configurations;

internal sealed class HolidayCalendarRowConfiguration : IEntityTypeConfiguration<HolidayCalendarRow>
{
    public void Configure(EntityTypeBuilder<HolidayCalendarRow> builder)
    {
        builder.ToTable("HolidayCalendars");

        builder.HasKey(calendar => calendar.Id);

        builder.Property(calendar => calendar.Year);

        // One calendar per year.
        builder.HasIndex(calendar => calendar.Year)
            .IsUnique();

        builder.Property(calendar => calendar.HolidaysJson)
            .HasColumnName("Holidays")
            .IsRequired();
    }
}
