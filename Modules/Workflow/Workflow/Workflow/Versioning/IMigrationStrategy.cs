using Workflow.Workflow.Models;

namespace Workflow.Workflow.Versioning;

/// <summary>
/// Strategy interface for migrating workflow instances between schema versions
/// </summary>
public interface IMigrationStrategy
{
    /// <summary>
    /// Name of the migration strategy
    /// </summary>
    string StrategyName { get; }

    /// <summary>
    /// Migrate a single workflow instance to the target version
    /// </summary>
    Task<bool> MigrateInstanceAsync(WorkflowInstance instance, string targetVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate that the instance can be migrated using this strategy
    /// </summary>
    Task<bool> CanMigrateAsync(WorkflowInstance instance, string targetVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get migration requirements for this strategy
    /// </summary>
    Task<MigrationRequirements> GetRequirementsAsync(string fromVersion, string toVersion, CancellationToken cancellationToken = default);
}

/// <summary>
/// Requirements for a migration strategy
/// </summary>
public class MigrationRequirements
{
    public bool RequiresDowntime { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public List<string> Prerequisites { get; set; } = new();
    public MigrationRisk RiskLevel { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
}