using Workflow.Workflow.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace Workflow.Data.Configurations;

public class WorkflowExternalCallConfiguration : IEntityTypeConfiguration<WorkflowExternalCall>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<WorkflowExternalCall> builder)
    {
        builder.ToTable("WorkflowExternalCalls");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActivityId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(x => x.Endpoint)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.Method)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.RequestPayload)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Headers)
            .HasConversion(
                v => JsonSerializer.Serialize(v, SerializerOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, SerializerOptions) ?? new Dictionary<string, string>())
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(x => x.ResponsePayload)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

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
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique();
        
        // Composite indexes for external call processing
        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("IX_WorkflowExternalCalls_Status_Created");
        builder.HasIndex(x => new { x.WorkflowInstanceId, x.ActivityId, x.Status })
            .HasDatabaseName("IX_WorkflowExternalCalls_Instance_Activity_Status");
    }
}