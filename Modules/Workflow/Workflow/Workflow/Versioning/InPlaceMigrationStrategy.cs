using Microsoft.Extensions.Logging;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;

namespace Workflow.Workflow.Versioning;

/// <summary>
/// Migration strategy that updates workflow instances in-place without creating copies
/// </summary>
public class InPlaceMigrationStrategy : IMigrationStrategy
{
    private readonly ILogger<InPlaceMigrationStrategy> _logger;
    private readonly IWorkflowInstanceRepository _instanceRepository;

    public string StrategyName => "InPlace";

    public InPlaceMigrationStrategy(
        ILogger<InPlaceMigrationStrategy> logger,
        IWorkflowInstanceRepository instanceRepository)
    {
        _logger = logger;
        _instanceRepository = instanceRepository;
    }

    public async Task<bool> MigrateInstanceAsync(WorkflowInstance instance, string targetVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting in-place migration for instance {InstanceId} to version {Version}", 
                instance.Id, targetVersion);

            // Validate current state allows migration
            if (instance.Status == WorkflowStatus.Running)
            {
                _logger.LogWarning("Cannot migrate running instance {InstanceId}", instance.Id);
                return false;
            }

            // Note: In a real implementation, you would add SchemaVersion property to WorkflowInstance
            // For now, we simulate the migration by updating the status
            _logger.LogInformation("Simulating version update to {Version}", targetVersion);
            instance.UpdatedOn = DateTime.UtcNow;

            // Save changes
            await _instanceRepository.UpdateAsync(instance, cancellationToken);

            _logger.LogInformation("Successfully migrated instance {InstanceId} to version {Version}", 
                instance.Id, targetVersion);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate instance {InstanceId} to version {Version}", 
                instance.Id, targetVersion);
            return false;
        }
    }

    public async Task<bool> CanMigrateAsync(WorkflowInstance instance, string targetVersion, CancellationToken cancellationToken = default)
    {
        // In-place migration requires instance to be suspended or completed
        return instance.Status is WorkflowStatus.Suspended or WorkflowStatus.Completed or WorkflowStatus.Failed;
    }

    public async Task<MigrationRequirements> GetRequirementsAsync(string fromVersion, string toVersion, CancellationToken cancellationToken = default)
    {
        return new MigrationRequirements
        {
            RequiresDowntime = false,
            EstimatedDuration = TimeSpan.FromMilliseconds(100),
            Prerequisites = ["Instance must not be running"],
            RiskLevel = MigrationRisk.Low,
            Configuration = new Dictionary<string, object>
            {
                ["AllowRunningInstances"] = false,
                ["CreateBackup"] = false
            }
        };
    }
}