using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Data.Configurations;

/// <summary>
/// Entity Framework configuration for WorkflowDefinitionVersion
/// </summary>
public class WorkflowDefinitionVersionConfiguration : IEntityTypeConfiguration<WorkflowDefinitionVersion>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinitionVersion> builder)
    {
        builder.ToTable("WorkflowDefinitionVersions", "workflow");

        // Primary Key
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .IsRequired();

        // Properties
        builder.Property(x => x.DefinitionId)
            .IsRequired()
            .HasColumnName("DefinitionId");

        builder.Property(x => x.Version)
            .IsRequired()
            .HasColumnName("Version");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnName("Name");

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(2000)
            .HasColumnName("Description");

        builder.Property(x => x.JsonSchema)
            .IsRequired()
            .HasColumnType("nvarchar(max)")
            .HasColumnName("JsonSchema");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasColumnName("Status");

        builder.Property(x => x.MigrationInstructions)
            .HasMaxLength(4000)
            .HasColumnName("MigrationInstructions");

        builder.Property(x => x.PublishedAt)
            .HasColumnName("PublishedAt");

        builder.Property(x => x.PublishedBy)
            .HasMaxLength(256)
            .HasColumnName("PublishedBy");

        builder.Property(x => x.DeprecatedAt)
            .HasColumnName("DeprecatedAt");

        builder.Property(x => x.DeprecatedBy)
            .HasMaxLength(256)
            .HasColumnName("DeprecatedBy");

        builder.Property(x => x.Category)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnName("Category");

        // JSON serialization for complex properties
        builder.Property(x => x.BreakingChanges)
            .HasColumnName("BreakingChanges")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<BreakingChange>>(v, (JsonSerializerOptions?)null) ?? new List<BreakingChange>()
            );

        builder.Property(x => x.Metadata)
            .HasColumnName("Metadata")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
            );

        // Base Entity properties
        builder.Property(x => x.CreatedOn)
            .IsRequired()
            .HasColumnName("CreatedOn")
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(x => x.CreatedBy)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnName("CreatedBy");

        builder.Property(x => x.UpdatedOn)
            .HasColumnName("UpdatedOn");

        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(256)
            .HasColumnName("UpdatedBy");

        // Indexes
        builder.HasIndex(x => x.DefinitionId)
            .HasDatabaseName("IX_WorkflowDefinitionVersions_DefinitionId");

        builder.HasIndex(x => new { x.DefinitionId, x.Version })
            .HasDatabaseName("IX_WorkflowDefinitionVersions_DefinitionId_Version")
            .IsUnique();

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_WorkflowDefinitionVersions_Status");

        builder.HasIndex(x => x.Category)
            .HasDatabaseName("IX_WorkflowDefinitionVersions_Category");

        builder.HasIndex(x => x.PublishedAt)
            .HasDatabaseName("IX_WorkflowDefinitionVersions_PublishedAt")
            .HasFilter("PublishedAt IS NOT NULL");

        builder.HasIndex(x => new { x.Status, x.DefinitionId, x.Version })
            .HasDatabaseName("IX_WorkflowDefinitionVersions_Status_DefinitionId_Version")
            .HasFilter("Status = 1"); // Only published versions
    }
}