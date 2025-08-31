using Assignment.Workflow.Models;
using Assignment.Workflow.Schema;
using Assignment.Workflow.Activities.Core;

namespace Assignment.Workflow.Engine;

/// <summary>
/// Manages workflow lifecycle operations - Core orchestration responsibility
/// Handles state transitions, lifecycle events, and execution flow coordination
/// </summary>
public class WorkflowLifecycleManager : IWorkflowLifecycleManager
{
    private readonly ILogger<WorkflowLifecycleManager> _logger;

    // Define valid state transitions
    private static readonly Dictionary<WorkflowStatus, HashSet<WorkflowStatus>> AllowedTransitions = new()
    {
        [WorkflowStatus.Running] = new HashSet<WorkflowStatus>
        {
            WorkflowStatus.Completed,
            WorkflowStatus.Failed,
            WorkflowStatus.Cancelled,
            WorkflowStatus.Suspended
        },
        [WorkflowStatus.Suspended] = new HashSet<WorkflowStatus>
        {
            WorkflowStatus.Running,
            WorkflowStatus.Cancelled
        },
        [WorkflowStatus.Completed] = new HashSet<WorkflowStatus>(), // Terminal state
        [WorkflowStatus.Failed] = new HashSet<WorkflowStatus>(), // Terminal state  
        [WorkflowStatus.Cancelled] = new HashSet<WorkflowStatus>() // Terminal state
    };

    public WorkflowLifecycleManager(ILogger<WorkflowLifecycleManager> logger)
    {
        _logger = logger;
    }

    public async Task<WorkflowInstance> InitializeWorkflowAsync(
        Guid workflowDefinitionId,
        WorkflowSchema workflowSchema,
        string instanceName,
        string startedBy,
        Dictionary<string, object>? initialVariables = null,
        string? correlationId = null,
        Dictionary<string, RuntimeOverride>? runtimeOverrides = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var workflowInstance = WorkflowInstance.Create(
                workflowDefinitionId,
                instanceName,
                correlationId,
                startedBy,
                initialVariables,
                runtimeOverrides);

            _logger.LogDebug("Initialized workflow instance {InstanceName}", instanceName);

            await Task.CompletedTask; // For future async operations

            return workflowInstance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize workflow instance {InstanceName}", instanceName);
            throw;
        }
    }

    public async Task TransitionWorkflowStateAsync(
        WorkflowInstance workflowInstance,
        WorkflowStatus newStatus,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanTransitionTo(workflowInstance.Status, newStatus))
        {
            throw new InvalidOperationException(
                $"Cannot transition workflow from {workflowInstance.Status} to {newStatus}");
        }

        var previousStatus = workflowInstance.Status;
        workflowInstance.UpdateStatus(newStatus, reason);

        _logger.LogInformation("Transitioned workflow {WorkflowInstanceId} from {PreviousStatus} to {NewStatus}",
            workflowInstance.Id, previousStatus, newStatus);

        await Task.CompletedTask; // For future async operations
    }

    public async Task AdvanceWorkflowAsync(
        WorkflowInstance workflowInstance,
        string nextActivityId,
        string? assignee = null,
        CancellationToken cancellationToken = default)
    {
        var previousActivityId = workflowInstance.CurrentActivityId;
        workflowInstance.SetCurrentActivity(nextActivityId, assignee);

        _logger.LogDebug("Advanced workflow {WorkflowInstanceId} from activity {PreviousActivity} to {NextActivity}",
            workflowInstance.Id, previousActivityId, nextActivityId);

        await Task.CompletedTask; // For future async operations
    }

    public async Task CompleteWorkflowAsync(
        WorkflowInstance workflowInstance,
        CancellationToken cancellationToken = default)
    {
        await TransitionWorkflowStateAsync(workflowInstance, WorkflowStatus.Completed,
            "Workflow completed successfully", cancellationToken);

        _logger.LogInformation("Completed workflow {WorkflowInstanceId}", workflowInstance.Id);
    }

    public async Task PauseWorkflowAsync(
        WorkflowInstance workflowInstance,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await TransitionWorkflowStateAsync(workflowInstance, WorkflowStatus.Suspended, reason, cancellationToken);

        _logger.LogInformation("Paused workflow {WorkflowInstanceId}: {Reason}", workflowInstance.Id, reason);
    }

    public async Task ResumeWorkflowAsync(
        WorkflowInstance workflowInstance,
        string resumedBy,
        CancellationToken cancellationToken = default)
    {
        await TransitionWorkflowStateAsync(workflowInstance, WorkflowStatus.Running, $"Resumed by {resumedBy}",
            cancellationToken);

        _logger.LogInformation("Resumed workflow {WorkflowInstanceId} by {ResumedBy}", workflowInstance.Id, resumedBy);
    }

    public async Task TerminateWorkflowAsync(
        WorkflowInstance workflowInstance,
        string reason,
        string terminatedBy,
        CancellationToken cancellationToken = default)
    {
        await TransitionWorkflowStateAsync(workflowInstance, WorkflowStatus.Cancelled,
            $"Terminated by {terminatedBy}: {reason}", cancellationToken);

        _logger.LogInformation("Terminated workflow {WorkflowInstanceId} by {TerminatedBy}: {Reason}",
            workflowInstance.Id, terminatedBy, reason);
    }

    public bool CanTransitionTo(WorkflowStatus currentStatus, WorkflowStatus targetStatus)
    {
        return AllowedTransitions.ContainsKey(currentStatus) &&
               AllowedTransitions[currentStatus].Contains(targetStatus);
    }
}