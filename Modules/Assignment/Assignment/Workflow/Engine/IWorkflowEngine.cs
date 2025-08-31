using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Models;
using Assignment.Workflow.Schema;

namespace Assignment.Workflow.Engine;

/// <summary>
/// Core workflow engine - Orchestration responsibilities
/// Handles task scheduling, execution coordination, and workflow lifecycle management
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// ORCHESTRATION: Executes a single activity and returns the result
    /// </summary>
    Task<ActivityResult> ExecuteActivityAsync(
        ActivityDefinition activityDefinition,
        ActivityContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ORCHESTRATION: Resumes a pending activity with input data and returns the result
    /// </summary>
    Task<ActivityResult> ResumeActivityAsync(
        ActivityDefinition activityDefinition,
        ActivityContext context,
        Dictionary<string, object> resumeInput,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ORCHESTRATION: Coordinates workflow execution from start to completion
    /// </summary>
    Task<WorkflowExecutionResult> ExecuteWorkflowAsync(
        WorkflowSchema workflowSchema,
        WorkflowInstance workflowInstance,
        ActivityDefinition startActivity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ORCHESTRATION: Coordinates workflow resumption from a pending activity
    /// </summary>
    Task<WorkflowExecutionResult> ResumeWorkflowExecutionAsync(
        WorkflowSchema workflowSchema,
        WorkflowInstance workflowInstance,
        ActivityDefinition currentActivity,
        Dictionary<string, object> resumeInput,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// HIGH-LEVEL ORCHESTRATION: Starts a complete workflow from definition
    /// WorkflowService should delegate to this method for all startup logic
    /// </summary>
    Task<WorkflowExecutionResult> StartWorkflowAsync(
        Guid workflowDefinitionId,
        string instanceName,
        string startedBy,
        Dictionary<string, object>? initialVariables = null,
        string? correlationId = null,
        Dictionary<string, RuntimeOverride>? assignmentOverrides = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// HIGH-LEVEL ORCHESTRATION: Resumes a workflow from a specific activity
    /// WorkflowService should delegate to this method for all resume logic
    /// </summary>
    Task<WorkflowExecutionResult> ResumeWorkflowAsync(
        Guid workflowInstanceId,
        string activityId,
        string completedBy,
        Dictionary<string, object>? input = null,
        Dictionary<string, RuntimeOverride>? nextAssignmentOverrides = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// VALIDATION: Validates workflow definition structure and activities
    /// </summary>
    Task<bool> ValidateWorkflowDefinitionAsync(
        WorkflowSchema workflowSchema,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of workflow execution containing outcome and next steps
/// </summary>
public class WorkflowExecutionResult
{
    public WorkflowExecutionStatus Status { get; init; }
    public WorkflowInstance? WorkflowInstance { get; init; }
    public string? NextActivityId { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object> Variables { get; init; } = new();
    public bool IsCompleted { get; init; }
    public bool RequiresExternalCompletion { get; init; }

    public static WorkflowExecutionResult Completed(WorkflowInstance workflowInstance)
        => new()
        {
            Status = WorkflowExecutionStatus.Completed, IsCompleted = true, WorkflowInstance = workflowInstance,
            Variables = workflowInstance.Variables
        };

    public static WorkflowExecutionResult Pending(WorkflowInstance workflowInstance, string nextActivityId)
        => new()
        {
            Status = WorkflowExecutionStatus.Pending, NextActivityId = nextActivityId,
            RequiresExternalCompletion = true, WorkflowInstance = workflowInstance,
            Variables = workflowInstance.Variables
        };

    public static WorkflowExecutionResult Failed(WorkflowInstance? workflowInstance, string errorMessage)
        => new()
        {
            Status = WorkflowExecutionStatus.Failed, ErrorMessage = errorMessage, WorkflowInstance = workflowInstance,
            Variables = workflowInstance?.Variables ?? new()
        };

    public static WorkflowExecutionResult Running(WorkflowInstance workflowInstance, string nextActivityId)
        => new()
        {
            Status = WorkflowExecutionStatus.Running, NextActivityId = nextActivityId,
            WorkflowInstance = workflowInstance, Variables = workflowInstance.Variables
        };
}

public enum WorkflowExecutionStatus
{
    Running,
    Completed,
    Failed,
    Pending
}