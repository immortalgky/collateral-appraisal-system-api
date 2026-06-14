using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shared.Scheduling;

/// <summary>
/// EF mapping for <see cref="JobSchedule"/>. No explicit schema — the table lands in the owning
/// DbContext's default schema, so each module gets its own <c>{schema}.JobSchedules</c>.
/// </summary>
public class JobScheduleConfiguration : IEntityTypeConfiguration<JobSchedule>
{
    public void Configure(EntityTypeBuilder<JobSchedule> builder)
    {
        builder.ToTable("JobSchedules");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(s => s.JobId)
            .IsRequired()
            .HasMaxLength(JobSchedule.MaxJobIdLength);

        builder.Property(s => s.CronExpression)
            .IsRequired()
            .HasMaxLength(JobSchedule.MaxCronLength);

        builder.Property(s => s.TimeZoneId)
            .HasMaxLength(JobSchedule.MaxTimeZoneIdLength);

        builder.Property(s => s.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(s => s.Description)
            .HasMaxLength(JobSchedule.MaxDescriptionLength);

        builder.HasIndex(s => s.JobId)
            .IsUnique()
            .HasDatabaseName("UX_JobSchedules_JobId");
    }
}
