using Workflow.Workflow.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Workflow.Data.Configurations;

public class WorkflowBookmarkConfiguration : IEntityTypeConfiguration<WorkflowBookmark>
{
    public void Configure(EntityTypeBuilder<WorkflowBookmark> builder)
    {
        builder.ToTable("WorkflowBookmarks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActivityId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CorrelationId)
            .HasMaxLength(100);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(x => x.Key)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Payload)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.IsConsumed)
            .IsRequired();

        builder.Property(x => x.ConsumedBy)
            .HasMaxLength(100);

        builder.Property(x => x.ClaimedBy)
            .HasMaxLength(100);

        builder.Property(x => x.ClaimedAt);

        builder.Property(x => x.LeaseExpiresAt);

        builder.Property(x => x.ConcurrencyToken)
            .IsRequired()
            .IsRowVersion();

        builder.HasOne(x => x.WorkflowInstance)
            .WithMany()
            .HasForeignKey(x => x.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for efficient querying
        builder.HasIndex(x => x.WorkflowInstanceId);
        builder.HasIndex(x => x.ActivityId);
        builder.HasIndex(x => x.Type);
        builder.HasIndex(x => x.IsConsumed);
        builder.HasIndex(x => x.DueAt);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.CorrelationId);
        builder.HasIndex(x => x.ClaimedBy);
        builder.HasIndex(x => x.LeaseExpiresAt);
        
        // Composite indexes for common queries
        builder.HasIndex(x => new { x.WorkflowInstanceId, x.ActivityId, x.IsConsumed })
            .HasDatabaseName("IX_WorkflowBookmarks_Instance_Activity_Consumed");
        builder.HasIndex(x => new { x.Type, x.IsConsumed, x.DueAt })
            .HasDatabaseName("IX_WorkflowBookmarks_Type_Consumed_Due");
        builder.HasIndex(x => new { x.Key, x.Type, x.IsConsumed })
            .HasDatabaseName("IX_WorkflowBookmarks_Key_Type_Consumed");
        builder.HasIndex(x => new { x.CorrelationId, x.Type, x.IsConsumed })
            .HasDatabaseName("IX_WorkflowBookmarks_Correlation_Type_Consumed");
        builder.HasIndex(x => new { x.Type, x.IsConsumed, x.ClaimedBy, x.LeaseExpiresAt })
            .HasDatabaseName("IX_WorkflowBookmarks_Type_Consumed_Claim");
    }
}