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
        // Nullable: not available when created via integration event (no cross-module AppraisalReview.Id).
        builder.Property(q => q.AppraisalDecisionId);
        builder.Property(q => q.CommitteeId).IsRequired();
        builder.Property(q => q.CommitteeCode).HasMaxLength(100).IsRequired();
        builder.Property(q => q.AcknowledgementGroup).HasMaxLength(100).IsRequired();
        builder.Property(q => q.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(q => q.MeetingId);
        builder.Property(q => q.EnqueuedAt).IsRequired();

        builder.HasIndex(q => q.Status);
        builder.HasIndex(q => q.MeetingId);
        // An appraisal can only be pending/included for a given committee once at a time.
        // AppraisalDecisionId is nullable (not always known), so the idempotency guard
        // uses (AppraisalId, CommitteeId) instead.
        builder.HasIndex(q => new { q.AppraisalId, q.CommitteeId })
            .IsUnique()
            .HasFilter("[Status] IN ('PendingAcknowledgement', 'Included')")
            .HasDatabaseName("UX_AckQueueItems_AppraisalId_CommitteeId_Active");
    }
}
