namespace Workflow.Workflow.Versioning;

/// <summary>
/// Service for managing workflow schema versioning and migrations between versions
/// </summary>
public interface IWorkflowVersioningService
{
    /// <summary>
    /// Get current schema version for a workflow definition
    /// </summary>
    Task<string> GetSchemaVersionAsync(string workflowDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compare two workflow schema versions and analyze differences
    /// </summary>
    Task<VersionComparisonResult> CompareVersionsAsync(string fromVersion, string toVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimate migration effort and breaking changes when upgrading schema versions
    /// </summary>
    Task<MigrationEstimate> EstimateVersionMigrationAsync(string fromVersion, string toVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Migrate workflow instances from one schema version to another
    /// </summary>
    Task<VersionMigrationResult> MigrateInstancesAsync(string workflowDefinitionId, string fromVersion, string toVersion, IMigrationStrategy strategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate that a workflow schema is compatible with a target version
    /// </summary>
    Task<bool> ValidateSchemaCompatibilityAsync(string workflowSchema, string targetVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all available schema versions for a workflow type
    /// </summary>
    Task<IEnumerable<string>> GetAvailableVersionsAsync(string workflowType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Register a new workflow schema version
    /// </summary>
    Task RegisterSchemaVersionAsync(string workflowType, string version, string schemaJson, CancellationToken cancellationToken = default);
}