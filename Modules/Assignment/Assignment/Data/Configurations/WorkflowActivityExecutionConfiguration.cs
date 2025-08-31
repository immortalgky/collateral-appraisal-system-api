using Assignment.Workflow.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Assignment.Data.Configurations;

public class WorkflowActivityExecutionConfiguration : IEntityTypeConfiguration<WorkflowActivityExecution>
{
    public void Configure(EntityTypeBuilder<WorkflowActivityExecution> builder)
    {
        builder.ToTable("WorkflowActivityExecutions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActivityId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ActivityName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ActivityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(x => x.AssignedTo)
            .HasMaxLength(100);

        builder.Property(x => x.CompletedBy)
            .HasMaxLength(100);

        builder.Property(x => x.InputData)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v,
                    (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, object>())
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.OutputData)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v,
                    (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, object>())
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.Comments)
            .HasMaxLength(1000);

        builder.HasOne(x => x.WorkflowInstance)
            .WithMany(x => x.ActivityExecutions)
            .HasForeignKey(x => x.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ActivityId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.AssignedTo);
        builder.HasIndex(x => x.StartedOn);
        
        // Optimized composite indexes for hybrid approach performance
        builder.HasIndex(x => new { x.WorkflowInstanceId, x.Status })
            .HasDatabaseName("IX_WorkflowActivityExecutions_WorkflowInstanceId_Status");
        builder.HasIndex(x => new { x.AssignedTo, x.Status })
            .HasDatabaseName("IX_WorkflowActivityExecutions_AssignedTo_Status");
    }
}