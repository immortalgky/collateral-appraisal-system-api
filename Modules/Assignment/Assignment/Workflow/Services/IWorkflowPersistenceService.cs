using Assignment.Workflow.Models;
using Assignment.Workflow.Schema;

namespace Assignment.Workflow.Services;

/// <summary>
/// Handles all workflow-related data persistence operations
/// Service Layer responsibility - manages database transactions and data operations
/// </summary>
public interface IWorkflowPersistenceService
{
    /// <summary>
    /// Retrieves workflow definition by ID
    /// </summary>
    Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deserializes and validates workflow schema from JSON
    /// </summary>
    Task<WorkflowSchema?> GetWorkflowSchemaAsync(Guid definitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new workflow instance with transaction management
    /// </summary>
    Task<WorkflowInstance> SaveWorkflowInstanceAsync(WorkflowInstance workflowInstance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing workflow instance
    /// </summary>
    Task UpdateWorkflowInstanceAsync(WorkflowInstance workflowInstance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves workflow instance by ID with all related data
    /// </summary>
    Task<WorkflowInstance?> GetWorkflowInstanceAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves workflow instance with execution history
    /// </summary>
    Task<WorkflowInstance?> GetWorkflowInstanceWithExecutionsAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and persists a new activity execution record
    /// </summary>
    Task<WorkflowActivityExecution> CreateActivityExecutionAsync(
        Guid workflowInstanceId,
        string activityId,
        string activityName,
        string activityType,
        string? assignedTo = null,
        Dictionary<string, object>? inputData = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an activity execution record
    /// </summary>
    Task UpdateActivityExecutionAsync(WorkflowActivityExecution activityExecution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves current activity execution for a workflow
    /// </summary>
    Task<WorkflowActivityExecution?> GetCurrentActivityExecutionAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves user tasks (workflows assigned to a specific user)
    /// </summary>
    Task<IEnumerable<WorkflowInstance>> GetUserTasksAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves current activities for a user
    /// </summary>
    Task<IEnumerable<WorkflowActivityExecution>> GetCurrentActivitiesForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all current activities
    /// </summary>
    Task<IEnumerable<WorkflowActivityExecution>> GetCurrentActivitiesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes multiple operations in a single database transaction
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);
}