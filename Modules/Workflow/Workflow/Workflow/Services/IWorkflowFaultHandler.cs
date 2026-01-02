using Workflow.Workflow.Models;

namespace Workflow.Workflow.Services;

public interface IWorkflowFaultHandler
{
    /// <summary>
    /// Handle workflow startup failure
    /// </summary>
    Task<FaultHandlingResult> HandleWorkflowStartupFaultAsync(
        StartWorkflowFaultContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handle workflow activity execution failure
    /// </summary>
    Task<FaultHandlingResult> HandleActivityExecutionFaultAsync(
        ActivityFaultContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handle external call failure
    /// </summary>
    Task<FaultHandlingResult> HandleExternalCallFaultAsync(
        ExternalCallFaultContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handle workflow resumption failure
    /// </summary>
    Task<FaultHandlingResult> HandleWorkflowResumeFaultAsync(
        WorkflowResumeFaultContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determine if workflow should be suspended due to repeated failures
    /// </summary>
    Task<bool> ShouldSuspendWorkflowAsync(
        Guid workflowInstanceId,
        string errorType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create compensation plan for failed workflow
    /// </summary>
    Task<CompensationPlan> CreateCompensationPlanAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default);
}

public sealed record FaultHandlingResult(
    bool ShouldRetry,
    bool SuspendWorkflow,
    bool RequiresManualIntervention,
    TimeSpan? RetryDelay,
    string? RecommendedAction,
    CompensationPlan? CompensationPlan = null
);

public sealed record StartWorkflowFaultContext(
    Guid WorkflowDefinitionId,
    string InstanceName,
    string StartedBy,
    Exception Exception,
    int AttemptNumber
);

public sealed record ActivityFaultContext(
    Guid WorkflowInstanceId,
    string ActivityId,
    string ActivityType,
    Exception Exception,
    int AttemptNumber,
    Dictionary<string, object> ActivityData
);

public sealed record ExternalCallFaultContext(
    Guid ExternalCallId,
    Guid WorkflowInstanceId,
    string ActivityId,
    ExternalCallType CallType,
    string Endpoint,
    Exception Exception,
    int AttemptNumber
);

public sealed record WorkflowResumeFaultContext(
    Guid WorkflowInstanceId,
    string ActivityId,
    string BookmarkKey,
    Exception Exception,
    int AttemptNumber
);

public sealed record CompensationPlan(
    Guid WorkflowInstanceId,
    List<CompensationStep> Steps,
    CompensationStrategy Strategy
);

public sealed record CompensationStep(
    string StepId,
    string Description,
    CompensationAction Action,
    Dictionary<string, object> Parameters,
    bool IsRequired
);

public enum CompensationStrategy
{
    Rollback,
    Forward,
    ManualIntervention,
    Ignore
}

public enum CompensationAction
{
    UndoActivity,
    ReverseExternalCall,
    SendNotification,
    LogError,
    CreateTask,
    UpdateStatus
}