using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Engine.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;

namespace Workflow.Workflow.Engine;

/// <summary>
/// Core workflow engine - Orchestration responsibilities
/// Handles task scheduling, execution coordination, and workflow lifecycle management
/// </summary>
public interface IWorkflowEngine
{
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
    /// ORCHESTRATION: Coordinates workflow execution from start to completion
    /// </summary>
    Task<WorkflowExecutionResult> ExecuteWorkflowAsync(
        WorkflowSchema workflowSchema,
        WorkflowInstance workflowInstance,
        ActivityDefinition activityToExecute,
        Dictionary<string, object>? resumeInput = null,
        bool isResume = false,
        CancellationToken cancellationToken = default);


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
    /// VALIDATION: Validates workflow definition structure and activities
    /// </summary>
    Task<bool> ValidateWorkflowDefinitionAsync(
        WorkflowSchema workflowSchema,
        CancellationToken cancellationToken = default);
}