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
        builder.Property(m => m.ScheduledAt);
        builder.Property(m => m.EndedAt);
        builder.Property(m => m.CancelledAt);

        builder.HasMany(m => m.Items)
            .WithOne()
            .HasForeignKey(i => i.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(m => m.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(m => m.Status);
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
        builder.Property(i => i.WorkflowInstanceId).IsRequired();
        builder.Property(i => i.ActivityId).HasMaxLength(200).IsRequired();
        builder.Property(i => i.AddedAt).IsRequired();

        builder.HasIndex(i => new { i.MeetingId, i.AppraisalId }).IsUnique();
    }
}
