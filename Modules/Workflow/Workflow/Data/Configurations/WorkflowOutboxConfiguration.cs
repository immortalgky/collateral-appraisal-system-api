using Workflow.Workflow.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace Workflow.Data.Configurations;

public class WorkflowOutboxConfiguration : IEntityTypeConfiguration<WorkflowOutbox>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<WorkflowOutbox> builder)
    {
        builder.ToTable("WorkflowOutboxes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OccurredAt)
            .IsRequired();

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Payload)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Headers)
            .HasConversion(
                v => JsonSerializer.Serialize(v, SerializerOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, SerializerOptions) ?? new Dictionary<string, string>())
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Attempts)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.CorrelationId)
            .HasMaxLength(100);

        builder.Property(x => x.ActivityId)
            .HasMaxLength(100);

        builder.Property(x => x.ConcurrencyToken)
            .IsRequired()
            .IsRowVersion();

        builder.HasOne(x => x.WorkflowInstance)
            .WithMany()
            .HasForeignKey(x => x.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes for efficient processing by OutboxDispatcher
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.NextAttemptAt);
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => x.Type);
        builder.HasIndex(x => x.WorkflowInstanceId);
        builder.HasIndex(x => x.CorrelationId);
        
        // Critical index for OutboxDispatcher processing
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt })
            .HasDatabaseName("IX_WorkflowOutboxes_Processing");
        
        // Index for retry logic
        builder.HasIndex(x => new { x.Status, x.Attempts, x.NextAttemptAt })
            .HasDatabaseName("IX_WorkflowOutboxes_Retry");
        
        // Index for monitoring and analytics
        builder.HasIndex(x => new { x.Type, x.Status, x.OccurredAt })
            .HasDatabaseName("IX_WorkflowOutboxes_Analytics");
    }
}