using Workflow.Workflow.Models;

namespace Workflow.Workflow.Repositories;

/// <summary>
/// Repository for managing workflow definition versions
/// </summary>
public interface IWorkflowDefinitionVersionRepository
{
    /// <summary>
    /// Adds a new workflow definition version
    /// </summary>
    Task<WorkflowDefinitionVersion> AddAsync(WorkflowDefinitionVersion version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing workflow definition version
    /// </summary>
    Task<WorkflowDefinitionVersion> UpdateAsync(WorkflowDefinitionVersion version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific version by definition ID and version number
    /// </summary>
    Task<WorkflowDefinitionVersion?> GetVersionAsync(Guid definitionId, int version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest version for a definition ID
    /// </summary>
    Task<WorkflowDefinitionVersion?> GetLatestVersionAsync(Guid definitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest published version for a definition ID
    /// </summary>
    Task<WorkflowDefinitionVersion?> GetLatestPublishedVersionAsync(Guid definitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all versions for a definition ID, ordered by version number
    /// </summary>
    Task<List<WorkflowDefinitionVersion>> GetAllVersionsAsync(Guid definitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets versions by status
    /// </summary>
    Task<List<WorkflowDefinitionVersion>> GetVersionsByStatusAsync(VersionStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all published versions across all definitions
    /// </summary>
    Task<List<WorkflowDefinitionVersion>> GetAllPublishedVersionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a version exists
    /// </summary>
    Task<bool> ExistsAsync(Guid definitionId, int version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets version by unique ID
    /// </summary>
    Task<WorkflowDefinitionVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a version (only if not published)
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets versions that are eligible for cleanup (old, deprecated, no active instances)
    /// </summary>
    Task<List<WorkflowDefinitionVersion>> GetVersionsEligibleForCleanupAsync(DateTime olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about versions
    /// </summary>
    Task<WorkflowVersionStatistics> GetVersionStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about workflow versions
/// </summary>
public class WorkflowVersionStatistics
{
    public int TotalVersions { get; set; }
    public int DraftVersions { get; set; }
    public int PublishedVersions { get; set; }
    public int DeprecatedVersions { get; set; }
    public int ArchivedVersions { get; set; }
    public int TotalDefinitions { get; set; }
    public Dictionary<string, int> CategoryCounts { get; set; } = new();
}