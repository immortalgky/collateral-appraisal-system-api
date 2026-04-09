using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Meetings.ReadModels;

namespace Workflow.Data.Configurations;

public class MeetingQueueItemConfiguration : IEntityTypeConfiguration<MeetingQueueItem>
{
    public void Configure(EntityTypeBuilder<MeetingQueueItem> builder)
    {
        builder.ToTable("MeetingQueueItems");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.AppraisalId).IsRequired();
        builder.Property(q => q.AppraisalNo).HasMaxLength(100);
        builder.Property(q => q.FacilityLimit).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(q => q.WorkflowInstanceId).IsRequired();
        builder.Property(q => q.ActivityId).HasMaxLength(200).IsRequired();
        builder.Property(q => q.MeetingId);
        builder.Property(q => q.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(q => q.EnqueuedAt).IsRequired();

        builder.HasIndex(q => q.Status);
        builder.HasIndex(q => q.MeetingId);

        // Filtered unique index: an appraisal can only be actively Assigned to one meeting.
        builder.HasIndex(q => q.AppraisalId)
            .IsUnique()
            .HasFilter("[Status] = 'Assigned'");
    }
}
