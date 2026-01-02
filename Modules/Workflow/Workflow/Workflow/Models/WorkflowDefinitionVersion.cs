using Shared.DDD;

namespace Workflow.Workflow.Models;

/// <summary>
/// Represents a specific version of a workflow definition with enhanced versioning capabilities
/// Supports side-by-side versioning, migration strategies, and breaking change tracking
/// </summary>
public class WorkflowDefinitionVersion : Entity<Guid>
{
    public Guid DefinitionId { get; private set; } = default!;  // Stable ID across all versions
    public int Version { get; private set; }                    // Auto-incrementing version number
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string JsonSchema { get; private set; } = default!;  // Workflow schema as JSON
    public VersionStatus Status { get; private set; }           // Lifecycle status
    public string? MigrationInstructions { get; private set; }  // Human-readable migration guide
    public List<BreakingChange> BreakingChanges { get; private set; } = new();
    public DateTime? PublishedAt { get; private set; }
    public string? PublishedBy { get; private set; }
    public DateTime? DeprecatedAt { get; private set; }
    public string? DeprecatedBy { get; private set; }
    public string Category { get; private set; } = default!;
    public Dictionary<string, object> Metadata { get; private set; } = new();

    private WorkflowDefinitionVersion()
    {
        // For EF Core
    }

    public static WorkflowDefinitionVersion Create(
        Guid definitionId,
        int version,
        string name,
        string description,
        string jsonSchema,
        string category,
        string createdBy,
        Dictionary<string, object>? metadata = null)
    {
        return new WorkflowDefinitionVersion
        {
            Id = Guid.NewGuid(),
            DefinitionId = definitionId,
            Version = version,
            Name = name,
            Description = description,
            JsonSchema = jsonSchema,
            Status = VersionStatus.Draft,
            Category = category,
            Metadata = metadata ?? new Dictionary<string, object>(),
            CreatedOn = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }

    public void Publish(string publishedBy, List<BreakingChange>? breakingChanges = null, string? migrationInstructions = null)
    {
        if (Status == VersionStatus.Published)
            throw new InvalidOperationException("Version is already published");

        Status = VersionStatus.Published;
        PublishedAt = DateTime.UtcNow;
        PublishedBy = publishedBy;
        BreakingChanges = breakingChanges ?? new List<BreakingChange>();
        MigrationInstructions = migrationInstructions;
    }

    public void Deprecate(string deprecatedBy, string? reason = null)
    {
        if (Status != VersionStatus.Published)
            throw new InvalidOperationException("Only published versions can be deprecated");

        Status = VersionStatus.Deprecated;
        DeprecatedAt = DateTime.UtcNow;
        DeprecatedBy = deprecatedBy;

        if (!string.IsNullOrEmpty(reason))
        {
            Metadata["DeprecationReason"] = reason;
        }
    }

    public void Archive(string archivedBy)
    {
        Status = VersionStatus.Archived;
        Metadata["ArchivedAt"] = DateTime.UtcNow;
        Metadata["ArchivedBy"] = archivedBy;
    }

    public void UpdateSchema(string jsonSchema, string updatedBy)
    {
        if (Status == VersionStatus.Published)
            throw new InvalidOperationException("Cannot modify published version schema");

        JsonSchema = jsonSchema;
        UpdatedOn = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void UpdateMetadata(string name, string description, Dictionary<string, object>? metadata, string updatedBy)
    {
        Name = name;
        Description = description;
        if (metadata != null)
        {
            Metadata = metadata;
        }
        UpdatedOn = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public bool HasBreakingChanges() => BreakingChanges.Any();

    public bool IsCompatibleWith(WorkflowDefinitionVersion otherVersion)
    {
        // Simple compatibility check - can be enhanced with more sophisticated logic
        return DefinitionId == otherVersion.DefinitionId && !HasBreakingChanges();
    }
}

public enum VersionStatus
{
    Draft = 0,      // Under development, not available for execution
    Published = 1,  // Active and available for new workflow instances
    Deprecated = 2, // Discouraged for new instances, existing instances continue
    Archived = 3    // Read-only, not available for new instances
}

/// <summary>
/// Represents a breaking change between workflow versions
/// </summary>
public class BreakingChange
{
    public string Type { get; set; } = default!;        // ActivityRemoved, ActivityRenamed, PropertyChanged, etc.
    public string Description { get; set; } = default!; // Human-readable description
    public string AffectedComponent { get; set; } = default!; // Activity ID, Property name, etc.
    public Dictionary<string, object> MigrationData { get; set; } = new(); // Data needed for migration
    public ChangeImpact Impact { get; set; } = ChangeImpact.Medium;

    public static BreakingChange ActivityRemoved(string activityId, string description, Dictionary<string, object>? migrationData = null)
    {
        return new BreakingChange
        {
            Type = "ActivityRemoved",
            Description = description,
            AffectedComponent = activityId,
            MigrationData = migrationData ?? new Dictionary<string, object>(),
            Impact = ChangeImpact.High
        };
    }

    public static BreakingChange ActivityRenamed(string oldId, string newId, string description)
    {
        return new BreakingChange
        {
            Type = "ActivityRenamed",
            Description = description,
            AffectedComponent = oldId,
            MigrationData = new Dictionary<string, object>
            {
                ["OldId"] = oldId,
                ["NewId"] = newId
            },
            Impact = ChangeImpact.Medium
        };
    }

    public static BreakingChange PropertyChanged(string activityId, string propertyName, string description, object? oldValue = null, object? newValue = null)
    {
        return new BreakingChange
        {
            Type = "PropertyChanged",
            Description = description,
            AffectedComponent = $"{activityId}.{propertyName}",
            MigrationData = new Dictionary<string, object>
            {
                ["ActivityId"] = activityId,
                ["PropertyName"] = propertyName,
                ["OldValue"] = oldValue ?? "null",
                ["NewValue"] = newValue ?? "null"
            },
            Impact = ChangeImpact.Medium
        };
    }
}

public enum ChangeImpact
{
    Low = 0,    // Minor changes, backward compatible
    Medium = 1, // Some adaptation needed, but non-breaking with migration
    High = 2,   // Major changes, requires careful migration planning
    Critical = 3 // Breaking changes that may require manual intervention
}