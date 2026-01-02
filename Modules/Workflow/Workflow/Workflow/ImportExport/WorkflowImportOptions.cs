namespace Workflow.Workflow.ImportExport;

/// <summary>
/// Options for workflow import operations
/// </summary>
public class WorkflowImportOptions
{
    /// <summary>
    /// Strategy for handling conflicts during import
    /// </summary>
    public ConflictResolutionStrategy ConflictResolution { get; set; } = ConflictResolutionStrategy.Fail;

    /// <summary>
    /// Whether to validate workflow definitions before import
    /// </summary>
    public bool ValidateBeforeImport { get; set; } = true;

    /// <summary>
    /// Whether to create backup before import
    /// </summary>
    public bool CreateBackup { get; set; } = true;

    /// <summary>
    /// Whether to import in a transaction (rollback on failure)
    /// </summary>
    public bool UseTransaction { get; set; } = true;

    /// <summary>
    /// Maximum number of workflows to import in a single operation
    /// </summary>
    public int MaxWorkflowCount { get; set; } = 100;

    /// <summary>
    /// Whether to preserve original workflow IDs
    /// </summary>
    public bool PreserveWorkflowIds { get; set; } = false;

    /// <summary>
    /// Whether to preserve timestamps
    /// </summary>
    public bool PreserveTimestamps { get; set; } = false;

    /// <summary>
    /// Whether to import only active versions
    /// </summary>
    public bool ActiveVersionsOnly { get; set; } = true;

    /// <summary>
    /// Decryption key for encrypted imports
    /// </summary>
    public string? DecryptionKey { get; set; }

    /// <summary>
    /// Custom import filters
    /// </summary>
    public Dictionary<string, object> ImportFilters { get; set; } = new();

    /// <summary>
    /// Timeout for import operation
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// User performing the import (for audit)
    /// </summary>
    public string? ImportedBy { get; set; }

    /// <summary>
    /// Custom name prefix for imported workflows
    /// </summary>
    public string? NamePrefix { get; set; }

    /// <summary>
    /// Custom name suffix for imported workflows
    /// </summary>
    public string? NameSuffix { get; set; }

    /// <summary>
    /// Whether to skip workflows that already exist
    /// </summary>
    public bool SkipExisting { get; set; } = false;

    /// <summary>
    /// Validate options before import
    /// </summary>
    public void Validate()
    {
        if (MaxWorkflowCount <= 0)
        {
            throw new InvalidOperationException("MaxWorkflowCount must be positive");
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Timeout must be positive");
        }

        if (ConflictResolution == ConflictResolutionStrategy.UseKey && string.IsNullOrEmpty(DecryptionKey))
        {
            throw new InvalidOperationException("DecryptionKey is required when using key-based conflict resolution");
        }
    }
}

/// <summary>
/// Strategies for handling conflicts during import
/// </summary>
public enum ConflictResolutionStrategy
{
    /// <summary>
    /// Fail the import operation on any conflict
    /// </summary>
    Fail,

    /// <summary>
    /// Skip conflicting items and continue
    /// </summary>
    Skip,

    /// <summary>
    /// Overwrite existing items with imported ones
    /// </summary>
    Overwrite,

    /// <summary>
    /// Create new versions of conflicting items
    /// </summary>
    CreateVersion,

    /// <summary>
    /// Merge conflicting items where possible
    /// </summary>
    Merge,

    /// <summary>
    /// Use decryption key to resolve conflicts
    /// </summary>
    UseKey,

    /// <summary>
    /// Ask user for resolution (interactive mode)
    /// </summary>
    Interactive
}