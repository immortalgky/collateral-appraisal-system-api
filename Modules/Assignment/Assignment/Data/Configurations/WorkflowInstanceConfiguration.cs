using Assignment.Workflow.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Assignment.Data.Configurations;

public class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
{
    public void Configure(EntityTypeBuilder<WorkflowInstance> builder)
    {
        builder.ToTable("WorkflowInstances");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);
            
        builder.Property(x => x.CorrelationId)
            .HasMaxLength(100);
            
        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>();
            
        builder.Property(x => x.CurrentActivityId)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(x => x.CurrentAssignee)
            .HasMaxLength(100);
            
        builder.Property(x => x.StartedBy)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(x => x.Variables)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, object>())
            .HasColumnType("nvarchar(max)");
            
        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);
            
        builder.HasOne(x => x.WorkflowDefinition)
            .WithMany()
            .HasForeignKey(x => x.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasMany(x => x.ActivityExecutions)
            .WithOne(x => x.WorkflowInstance)
            .HasForeignKey(x => x.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasIndex(x => x.CorrelationId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CurrentAssignee);
        builder.HasIndex(x => x.StartedOn);
    }
}