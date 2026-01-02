using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Services;
using System.Data;

namespace Workflow.Workflow.Engine;

/// <summary>
/// Manages workflow lifecycle operations - Core orchestration responsibility
/// Handles state transitions, lifecycle events, and execution flow coordination
/// </summary>
public class WorkflowLifecycleManager : IWorkflowLifecycleManager
{
    private readonly IWorkflowStateManager _stateManager;
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

    public WorkflowLifecycleManager(
        IWorkflowStateManager stateManager,
        ILogger<WorkflowLifecycleManager> logger)
    {
        _stateManager = stateManager;
        _logger = logger;
    }

    public WorkflowInstance InitializeWorkflowAsync(
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

            return workflowInstance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize workflow instance {InstanceName}", instanceName);
            throw;
        }
    }

    public async Task<bool> TransitionWorkflowStateAsync(
        WorkflowInstance workflowInstance,
        WorkflowStatus newStatus,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!CanTransitionTo(workflowInstance.Status, newStatus))
                throw new InvalidOperationException(
                    $"Cannot transition workflow from {workflowInstance.Status} to {newStatus}");

            var previousStatus = workflowInstance.Status;
            workflowInstance.UpdateStatus(newStatus, reason);

            _logger.LogInformation(
                "Transitioned workflow {WorkflowInstanceId} from {PreviousStatus} to {NewStatus} (in-memory)",
                workflowInstance.Id, previousStatus, newStatus);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transition workflow {WorkflowInstanceId} to {Status}", workflowInstance.Id,
                newStatus);

            return false;
        }
    }

    public async Task<bool> AdvanceWorkflowAsync(
        WorkflowInstance workflowInstance,
        string nextActivityId,
        string? assignee = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var previousActivityId = workflowInstance.CurrentActivityId;
            workflowInstance.SetCurrentActivity(nextActivityId, assignee);

            _logger.LogDebug(
                "Advanced workflow {WorkflowInstanceId} from activity {PreviousActivity} to {NextActivity}",
                workflowInstance.Id, previousActivityId, nextActivityId);

            await Task.CompletedTask; // For future async operations

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to advance workflow {WorkflowInstanceId} to activity {NextActivity}",
                workflowInstance.Id, nextActivityId);

            return false;
        }
    }

    public async Task<bool> CompleteWorkflowAsync(
        WorkflowInstance workflowInstance,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await TransitionWorkflowStateAsync(workflowInstance, WorkflowStatus.Completed,
                "Workflow completed successfully", cancellationToken);

            _logger.LogInformation("Completed workflow {WorkflowInstanceId} (in-memory)", workflowInstance.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete workflow {WorkflowInstanceId}", workflowInstance.Id);

            return false;
        }
    }

    public async Task<bool> PauseWorkflowAsync(
        WorkflowInstance workflowInstance,
        string reason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await TransitionWorkflowStateAsync(workflowInstance, WorkflowStatus.Suspended, reason, cancellationToken);

            _logger.LogInformation("Paused workflow {WorkflowInstanceId}: {Reason} (in-memory)", workflowInstance.Id,
                reason);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause workflow {WorkflowInstanceId}", workflowInstance.Id);
            return false;
        }
    }

    public async Task<bool> ResumeWorkflowAsync(
        WorkflowInstance workflowInstance,
        string reason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await TransitionWorkflowStateAsync(workflowInstance, WorkflowStatus.Running, reason, cancellationToken);

            _logger.LogInformation("Resumed workflow {WorkflowInstanceId} (in-memory)", workflowInstance.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume workflow {WorkflowInstanceId}", workflowInstance.Id);
            return false;
        }
    }

    public async Task<bool> TerminateWorkflowAsync(
        WorkflowInstance workflowInstance,
        string reason,
        string terminatedBy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await TransitionWorkflowStateAsync(workflowInstance, WorkflowStatus.Cancelled,
                $"Terminated by {terminatedBy}: {reason}", cancellationToken);

            _logger.LogInformation("Terminated workflow {WorkflowInstanceId} by {TerminatedBy}: {Reason} (in-memory)",
                workflowInstance.Id, terminatedBy, reason);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to terminate workflow {WorkflowInstanceId}", workflowInstance.Id);
            return false;
        }
    }

    public bool CanTransitionTo(WorkflowStatus currentStatus, WorkflowStatus targetStatus)
    {
        return AllowedTransitions.ContainsKey(currentStatus) &&
               AllowedTransitions[currentStatus].Contains(targetStatus);
    }
}