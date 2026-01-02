using Workflow.Workflow.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace Workflow.Data.Configurations;

public class WorkflowExecutionLogConfiguration : IEntityTypeConfiguration<WorkflowExecutionLog>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<WorkflowExecutionLog> builder)
    {
        builder.ToTable("WorkflowExecutionLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActivityId)
            .HasMaxLength(100);

        builder.Property(x => x.Event)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(x => x.OccurredAt)
            .IsRequired();

        builder.Property(x => x.Details)
            .HasMaxLength(2000);

        builder.Property(x => x.ActorId)
            .HasMaxLength(100);

        builder.Property(x => x.CorrelationId)
            .HasMaxLength(100);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.Metadata)
            .HasConversion(
                v => JsonSerializer.Serialize(v, SerializerOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, SerializerOptions) ?? new Dictionary<string, object>())
            .HasColumnType("nvarchar(max)");

        builder.HasOne(x => x.WorkflowInstance)
            .WithMany()
            .HasForeignKey(x => x.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for efficient querying and analytics
        builder.HasIndex(x => x.WorkflowInstanceId);
        builder.HasIndex(x => x.ActivityId);
        builder.HasIndex(x => x.Event);
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => x.ActorId);
        builder.HasIndex(x => x.CorrelationId);
        
        // Composite indexes for common queries
        builder.HasIndex(x => new { x.WorkflowInstanceId, x.OccurredAt })
            .HasDatabaseName("IX_WorkflowExecutionLogs_Instance_Occurred");
        builder.HasIndex(x => new { x.Event, x.OccurredAt })
            .HasDatabaseName("IX_WorkflowExecutionLogs_Event_Occurred");
        builder.HasIndex(x => new { x.ActivityId, x.Event, x.OccurredAt })
            .HasDatabaseName("IX_WorkflowExecutionLogs_Activity_Event_Occurred");
        
        // Index for performance monitoring and analytics
        builder.HasIndex(x => new { x.OccurredAt, x.Event, x.WorkflowInstanceId })
            .HasDatabaseName("IX_WorkflowExecutionLogs_Analytics");
    }
}