using Workflow.Workflow.Models;

namespace Workflow.Workflow.Engine.Core;

/// <summary>
/// Result of workflow execution containing an outcome and next steps
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
    {
        return new WorkflowExecutionResult
        {
            Status = WorkflowExecutionStatus.Completed, IsCompleted = true, WorkflowInstance = workflowInstance,
            Variables = workflowInstance.Variables
        };
    }

    public static WorkflowExecutionResult Pending(WorkflowInstance workflowInstance, string nextActivityId)
    {
        return new WorkflowExecutionResult
        {
            Status = WorkflowExecutionStatus.Pending, NextActivityId = nextActivityId,
            RequiresExternalCompletion = true, WorkflowInstance = workflowInstance,
            Variables = workflowInstance.Variables
        };
    }

    public static WorkflowExecutionResult Failed(WorkflowInstance? workflowInstance, string errorMessage)
    {
        return new WorkflowExecutionResult
        {
            Status = WorkflowExecutionStatus.Failed, ErrorMessage = errorMessage, WorkflowInstance = workflowInstance,
            Variables = workflowInstance?.Variables ?? new Dictionary<string, object>()
        };
    }

    public static WorkflowExecutionResult Running(WorkflowInstance workflowInstance, string nextActivityId)
    {
        return new WorkflowExecutionResult
        {
            Status = WorkflowExecutionStatus.Running, NextActivityId = nextActivityId,
            WorkflowInstance = workflowInstance, Variables = workflowInstance.Variables
        };
    }
}