using Workflow.Services.Configuration.Models;

namespace Workflow.Services.Configuration;

/// <summary>
/// Service for managing external task assignment configurations
/// </summary>
public interface ITaskConfigurationService
{
    /// <summary>
    /// Gets the most specific active task assignment configuration for an activity.
    /// Resolution is most-specific-wins across the optional workflow + banking-segment scopes.
    /// </summary>
    /// <param name="activityId">The activity identifier</param>
    /// <param name="workflowDefinitionId">Optional workflow definition identifier for more specific lookup</param>
    /// <param name="bankingSegment">Optional banking segment (e.g. Retail, IBG); empty/whitespace is treated as no segment</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task assignment configuration or null if none found</returns>
    Task<TaskAssignmentConfigurationDto?> GetConfigurationAsync(
        string activityId,
        string? workflowDefinitionId = null,
        string? bankingSegment = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single configuration by its identifier, or null if not found.
    /// </summary>
    Task<TaskAssignmentConfigurationDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists configurations, optionally filtered by activity and/or banking segment.
    /// Used by the admin surface (returns active and inactive rows).
    /// </summary>
    Task<List<TaskAssignmentConfigurationDto>> ListConfigurationsAsync(
        string? activityId = null,
        string? bankingSegment = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new task assignment configuration
    /// </summary>
    /// <param name="request">Configuration creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created configuration</returns>
    Task<TaskAssignmentConfigurationDto> CreateConfigurationAsync(
        CreateTaskAssignmentConfigurationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing task assignment configuration
    /// </summary>
    /// <param name="id">Configuration identifier</param>
    /// <param name="request">Configuration update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated configuration</returns>
    Task<TaskAssignmentConfigurationDto> UpdateConfigurationAsync(
        Guid id,
        UpdateTaskAssignmentConfigurationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a task assignment configuration
    /// </summary>
    /// <param name="id">Configuration identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteConfigurationAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all configurations for a workflow definition
    /// </summary>
    /// <param name="workflowDefinitionId">Workflow definition identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of configurations</returns>
    Task<List<TaskAssignmentConfigurationDto>> GetConfigurationsByWorkflowAsync(
        string workflowDefinitionId,
        CancellationToken cancellationToken = default);
}