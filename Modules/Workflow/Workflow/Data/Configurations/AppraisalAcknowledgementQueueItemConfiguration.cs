using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Meetings.ReadModels;

namespace Workflow.Data.Configurations;

public class AppraisalAcknowledgementQueueItemConfiguration
    : IEntityTypeConfiguration<AppraisalAcknowledgementQueueItem>
{
    public void Configure(EntityTypeBuilder<AppraisalAcknowledgementQueueItem> builder)
    {
        builder.ToTable("AppraisalAcknowledgementQueueItems");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.AppraisalId).IsRequired();
        builder.Property(q => q.AppraisalNo).HasMaxLength(100);
        builder.Property(q => q.AppraisalDecisionId).IsRequired();
        builder.Property(q => q.CommitteeId).IsRequired();
        builder.Property(q => q.CommitteeCode).HasMaxLength(100).IsRequired();
        builder.Property(q => q.AcknowledgementGroup).HasMaxLength(100).IsRequired();
        builder.Property(q => q.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(q => q.MeetingId);
        builder.Property(q => q.EnqueuedAt).IsRequired();

        builder.HasIndex(q => q.Status);
        builder.HasIndex(q => q.MeetingId);
        // An appraisal decision can only be pending/included in one meeting at a time.
        builder.HasIndex(q => q.AppraisalDecisionId)
            .IsUnique()
            .HasFilter("[Status] IN ('PendingAcknowledgement', 'Included')");
    }
}
