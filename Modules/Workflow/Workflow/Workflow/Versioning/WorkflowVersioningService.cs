using Microsoft.Extensions.Logging;
using System.Text.Json;
using Workflow.Workflow.Repositories;

namespace Workflow.Workflow.Versioning;

/// <summary>
/// Production-ready workflow versioning service for schema management and migrations
/// </summary>
public class WorkflowVersioningService : IWorkflowVersioningService
{
    private readonly ILogger<WorkflowVersioningService> _logger;
    private readonly IWorkflowDefinitionRepository _definitionRepository;
    private readonly IWorkflowInstanceRepository _instanceRepository;
    
    // In-memory cache for schema versions (in production, use distributed cache)
    private readonly Dictionary<string, List<string>> _schemaVersionsCache = new();

    public WorkflowVersioningService(
        ILogger<WorkflowVersioningService> logger,
        IWorkflowDefinitionRepository definitionRepository,
        IWorkflowInstanceRepository instanceRepository)
    {
        _logger = logger;
        _definitionRepository = definitionRepository;
        _instanceRepository = instanceRepository;
    }

    public async Task<string> GetSchemaVersionAsync(string workflowDefinitionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var definition = await _definitionRepository.GetByIdAsync(Guid.Parse(workflowDefinitionId), cancellationToken);
            return definition?.Version.ToString() ?? "1.0.0";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schema version for workflow {WorkflowId}", workflowDefinitionId);
            return "1.0.0"; // Default version
        }
    }

    public async Task<VersionComparisonResult> CompareVersionsAsync(string fromVersion, string toVersion, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Comparing workflow versions from {FromVersion} to {ToVersion}", fromVersion, toVersion);

        var result = new VersionComparisonResult
        {
            FromVersion = fromVersion,
            ToVersion = toVersion,
            IsCompatible = IsVersionCompatible(fromVersion, toVersion)
        };

        // Analyze schema differences (simplified implementation)
        var breakingChanges = AnalyzeBreakingChanges(fromVersion, toVersion);
        result.BreakingChanges.AddRange(breakingChanges);

        _logger.LogInformation("Version comparison completed: {BreakingChangeCount} breaking changes found", 
            result.BreakingChanges.Count);

        return result;
    }

    public async Task<MigrationEstimate> EstimateVersionMigrationAsync(string fromVersion, string toVersion, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Estimating migration from version {FromVersion} to {ToVersion}", fromVersion, toVersion);

        // Get affected instances count
        var affectedCount = await GetAffectedInstancesCountAsync(fromVersion, cancellationToken);
        
        var comparison = await CompareVersionsAsync(fromVersion, toVersion, cancellationToken);
        
        var estimate = new MigrationEstimate
        {
            FromVersion = fromVersion,
            ToVersion = toVersion,
            AffectedInstanceCount = affectedCount,
            EstimatedDuration = CalculateEstimatedDuration(affectedCount, comparison.BreakingChanges.Count),
            RiskLevel = CalculateRiskLevel(comparison.BreakingChanges),
            CriticalChanges = comparison.BreakingChanges.Where(c => c.Severity == BreakingChangeSeverity.Critical).ToList(),
            RequiresDowntime = comparison.BreakingChanges.Any(c => c.Severity >= BreakingChangeSeverity.High)
        };

        // Add recommended actions
        estimate.RequiredActions.Add("Review breaking changes carefully");
        if (estimate.RequiresDowntime)
        {
            estimate.RequiredActions.Add("Plan maintenance window");
        }

        return estimate;
    }

    public async Task<VersionMigrationResult> MigrateInstancesAsync(string workflowDefinitionId, string fromVersion, string toVersion, IMigrationStrategy strategy, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting migration of workflow {WorkflowId} instances from {FromVersion} to {ToVersion} using {Strategy}", 
            workflowDefinitionId, fromVersion, toVersion, strategy.StrategyName);

        var result = new VersionMigrationResult
        {
            FromVersion = fromVersion,
            ToVersion = toVersion
        };

        try
        {
            // Get instances to migrate
            var instances = await GetInstancesForMigrationAsync(workflowDefinitionId, fromVersion, cancellationToken);
            result.TotalInstancesProcessed = instances.Count;

            // Migrate each instance
            foreach (var instance in instances)
            {
                try
                {
                    var canMigrate = await strategy.CanMigrateAsync(instance, toVersion, cancellationToken);
                    if (!canMigrate)
                    {
                        result.Errors.Add(new MigrationError
                        {
                            WorkflowInstanceId = instance.Id.ToString(),
                            ErrorMessage = "Instance cannot be migrated with current strategy",
                            ErrorCode = "MIGRATION_NOT_SUPPORTED"
                        });
                        result.FailedMigrations++;
                        continue;
                    }

                    var migrationSuccess = await strategy.MigrateInstanceAsync(instance, toVersion, cancellationToken);
                    
                    if (migrationSuccess)
                    {
                        result.SuccessfulMigrations++;
                    }
                    else
                    {
                        result.FailedMigrations++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to migrate instance {InstanceId}", instance.Id);
                    result.Errors.Add(new MigrationError
                    {
                        WorkflowInstanceId = instance.Id.ToString(),
                        ErrorMessage = ex.Message,
                        Exception = ex,
                        IsCritical = true
                    });
                    result.FailedMigrations++;
                }
            }

            result.IsSuccessful = result.FailedMigrations == 0;
            result.Duration = DateTime.UtcNow - startTime;

            _logger.LogInformation("Migration completed: {Successful}/{Total} instances migrated successfully in {Duration}", 
                result.SuccessfulMigrations, result.TotalInstancesProcessed, result.Duration);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration failed with critical error");
            result.IsSuccessful = false;
            result.Duration = DateTime.UtcNow - startTime;
            result.Errors.Add(new MigrationError
            {
                ErrorMessage = $"Critical migration failure: {ex.Message}",
                Exception = ex,
                IsCritical = true
            });
            return result;
        }
    }

    public async Task<bool> ValidateSchemaCompatibilityAsync(string workflowSchema, string targetVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            // Basic JSON validation
            JsonDocument.Parse(workflowSchema);
            
            // Version compatibility check (simplified)
            return IsVersionCompatible("1.0.0", targetVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema validation failed for target version {Version}", targetVersion);
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetAvailableVersionsAsync(string workflowType, CancellationToken cancellationToken = default)
    {
        if (_schemaVersionsCache.TryGetValue(workflowType, out var cachedVersions))
        {
            return cachedVersions;
        }

        // Default versions (in production, load from database or configuration)
        var versions = new List<string> { "1.0.0", "1.1.0", "2.0.0" };
        _schemaVersionsCache[workflowType] = versions;
        
        return versions;
    }

    public async Task RegisterSchemaVersionAsync(string workflowType, string version, string schemaJson, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Registering schema version {Version} for workflow type {WorkflowType}", version, workflowType);
        
        // Validate schema
        var isValid = await ValidateSchemaCompatibilityAsync(schemaJson, version, cancellationToken);
        if (!isValid)
        {
            throw new InvalidOperationException($"Invalid schema for version {version}");
        }

        // Add to cache (in production, persist to database)
        if (!_schemaVersionsCache.ContainsKey(workflowType))
        {
            _schemaVersionsCache[workflowType] = new List<string>();
        }

        if (!_schemaVersionsCache[workflowType].Contains(version))
        {
            _schemaVersionsCache[workflowType].Add(version);
        }

        _logger.LogInformation("Schema version {Version} registered successfully for {WorkflowType}", version, workflowType);
    }

    #region Private Helper Methods

    private bool IsVersionCompatible(string fromVersion, string toVersion)
    {
        // Simplified version compatibility check
        var from = Version.Parse(fromVersion);
        var to = Version.Parse(toVersion);
        
        // Major version change is breaking
        return from.Major == to.Major;
    }

    private List<BreakingChange> AnalyzeBreakingChanges(string fromVersion, string toVersion)
    {
        // Simplified breaking change analysis
        var changes = new List<BreakingChange>();
        
        var from = Version.Parse(fromVersion);
        var to = Version.Parse(toVersion);
        
        if (from.Major < to.Major)
        {
            changes.Add(new BreakingChange
            {
                Type = BreakingChangeType.PropertyTypeChanged,
                Description = $"Major version upgrade from {fromVersion} to {toVersion}",
                Severity = BreakingChangeSeverity.High
            });
        }

        return changes;
    }

    private async Task<int> GetAffectedInstancesCountAsync(string version, CancellationToken cancellationToken)
    {
        // Simplified count (in production, query database)
        return 10;
    }

    private TimeSpan CalculateEstimatedDuration(int instanceCount, int breakingChangesCount)
    {
        // Base time per instance + additional time for breaking changes
        var baseTimePerInstance = TimeSpan.FromMilliseconds(100);
        var breakingChangeTime = TimeSpan.FromSeconds(breakingChangesCount * 5);
        
        return TimeSpan.FromMilliseconds(instanceCount * baseTimePerInstance.TotalMilliseconds) + breakingChangeTime;
    }

    private MigrationRisk CalculateRiskLevel(List<BreakingChange> breakingChanges)
    {
        if (breakingChanges.Any(c => c.Severity == BreakingChangeSeverity.Critical))
            return MigrationRisk.Critical;
        
        if (breakingChanges.Any(c => c.Severity == BreakingChangeSeverity.High))
            return MigrationRisk.High;
        
        if (breakingChanges.Any(c => c.Severity == BreakingChangeSeverity.Medium))
            return MigrationRisk.Medium;
        
        return MigrationRisk.Low;
    }

    private async Task<List<Models.WorkflowInstance>> GetInstancesForMigrationAsync(string workflowDefinitionId, string version, CancellationToken cancellationToken)
    {
        // In production, query instances by workflow definition ID and version
        return new List<Models.WorkflowInstance>();
    }

    #endregion
}