using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Meetings.Domain;

namespace Workflow.Data.Configurations;

public class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.ToTable("Meetings");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Title).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Location).HasMaxLength(500);
        builder.Property(m => m.Notes).HasMaxLength(2000);
        builder.Property(m => m.CancelReason).HasMaxLength(1000);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.StartAt);
        builder.Property(m => m.EndAt);
        builder.Property(m => m.MeetingNo).HasMaxLength(50);
        builder.Property(m => m.MeetingNoYear);
        builder.Property(m => m.MeetingNoSeq);
        builder.Property(m => m.FromText).HasMaxLength(200);
        builder.Property(m => m.ToText).HasMaxLength(200);
        builder.Property(m => m.AgendaCertifyMinutes).HasMaxLength(2000);
        builder.Property(m => m.AgendaChairmanInformed).HasMaxLength(2000);
        builder.Property(m => m.AgendaOthers).HasMaxLength(2000);
        builder.Property(m => m.CutOffAt);
        builder.Property(m => m.InvitationSentAt);
        builder.Property(m => m.RowVersion).IsRowVersion();
        builder.Property(m => m.EndedAt);
        builder.Property(m => m.CancelledAt);

        builder.HasMany(m => m.Items)
            .WithOne()
            .HasForeignKey(i => i.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(m => m.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(m => m.Members)
            .WithOne()
            .HasForeignKey(mm => mm.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(m => m.Members).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => m.MeetingNo).IsUnique().HasFilter("[MeetingNo] IS NOT NULL");
    }
}

public class MeetingItemConfiguration : IEntityTypeConfiguration<MeetingItem>
{
    public void Configure(EntityTypeBuilder<MeetingItem> builder)
    {
        builder.ToTable("MeetingItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.MeetingId).IsRequired();
        builder.Property(i => i.AppraisalId).IsRequired();
        builder.Property(i => i.AppraisalNo).HasMaxLength(100);
        builder.Property(i => i.FacilityLimit).HasColumnType("decimal(18,2)").IsRequired();
        // Nullable: only set for Decision items; Acknowledgement items have null values.
        builder.Property(i => i.WorkflowInstanceId);
        builder.Property(i => i.ActivityId).HasMaxLength(200);
        builder.Property(i => i.AddedAt).IsRequired();

        // New columns (Phase 1)
        builder.Property(i => i.Kind).HasConversion<string>().HasMaxLength(30).IsRequired()
            .HasDefaultValue(MeetingItemKind.Decision);
        builder.Property(i => i.AppraisalType).HasMaxLength(50);
        builder.Property(i => i.AcknowledgementGroup).HasMaxLength(100);
        builder.Property(i => i.SourceAppraisalDecisionId);
        builder.Property(i => i.ItemDecision).HasConversion<string>().HasMaxLength(20).IsRequired()
            .HasDefaultValue(ItemDecision.Pending);
        builder.Property(i => i.DecisionAt);
        builder.Property(i => i.DecisionBy).HasMaxLength(255);
        builder.Property(i => i.DecisionReason).HasMaxLength(1000);

        builder.HasIndex(i => new { i.MeetingId, i.AppraisalId }).IsUnique();
    }
}

public class MeetingMemberConfiguration : IEntityTypeConfiguration<MeetingMember>
{
    public void Configure(EntityTypeBuilder<MeetingMember> builder)
    {
        builder.ToTable("MeetingMembers");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.MeetingId).IsRequired();
        builder.Property(m => m.UserId).HasMaxLength(255).IsRequired();
        builder.Property(m => m.MemberName).HasMaxLength(255).IsRequired();
        builder.Property(m => m.Position).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(m => m.SourceCommitteeMemberId);
        builder.Property(m => m.AddedAt).IsRequired();
    }
}

