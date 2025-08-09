using Assignment.Workflow.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Assignment.Data.Configurations;

public class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.ToTable("WorkflowDefinitions");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);
            
        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(1000);
            
        builder.Property(x => x.Category)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(x => x.JsonDefinition)
            .IsRequired()
            .HasColumnType("nvarchar(max)");
            
        builder.Property(x => x.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(100);
            
        builder.HasIndex(x => new { x.Name, x.Version })
            .IsUnique();
            
        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => x.IsActive);
    }
}