using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Workflow.Tasks.Models;
using Workflow.Workflow.Engine;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Engine.Core;

namespace Workflow.Workflow.Services;

/// <summary>
/// Workflow service - handles orchestration, validation, persistence, and events
/// </summary>
public class WorkflowService : IWorkflowService
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowPersistenceService _persistenceService;
    private readonly IWorkflowEventPublisher _eventPublisher;
    private readonly IWorkflowUnitOfWork _unitOfWork;
    private readonly IIntegrationEventOutbox _outbox;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(
        IWorkflowEngine workflowEngine,
        IWorkflowPersistenceService persistenceService,
        IWorkflowEventPublisher eventPublisher,
        IWorkflowUnitOfWork unitOfWork,
        IIntegrationEventOutbox outbox,
        IAssignmentRepository assignmentRepository,
        IDateTimeProvider dateTimeProvider,
        ILogger<WorkflowService> logger)
    {
        _workflowEngine = workflowEngine;
        _persistenceService = persistenceService;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
        _outbox = outbox;
        _assignmentRepository = assignmentRepository;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<WorkflowInstance> StartWorkflowAsync(
        Guid workflowDefinitionId,
        string instanceName,
        string startedBy,
        Dictionary<string, object>? initialVariables = null,
        string? correlationId = null,
        Dictionary<string, RuntimeOverride>? assignmentOverrides = null,
        CancellationToken cancellationToken = default)
    {
        // If already in a transaction (e.g., from MediatR TransactionalBehavior), run directly
        // Note: integration events will be published by the caller after commit
        if (_unitOfWork.HasActiveTransaction)
        {
            var instance = await ExecuteStartWorkflowAsync(workflowDefinitionId, instanceName, startedBy,
                initialVariables, correlationId, assignmentOverrides, cancellationToken);
            // TransactionalBehavior commits — we can't publish here (still in transaction)
            // Integration events deferred to post-commit in the non-transaction path
            return instance;
        }

        // Otherwise wrap in execution strategy + transaction (required for SqlServerRetryingExecutionStrategy)
        WorkflowInstance? result = null;
        var strategy = _unitOfWork.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                result = await ExecuteStartWorkflowAsync(workflowDefinitionId, instanceName, startedBy,
                    initialVariables, correlationId, assignmentOverrides, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                try { await _unitOfWork.RollbackTransactionAsync(cancellationToken); }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "SERVICE: Failed to rollback transaction");
                }
                throw;
            }
        });

        // Publish integration events AFTER transaction committed successfully
        await PublishPostCommitEventsAsync(result!, cancellationToken);

        return result!;
    }

    private async Task<WorkflowInstance> ExecuteStartWorkflowAsync(
        Guid workflowDefinitionId, string instanceName, string startedBy,
        Dictionary<string, object>? initialVariables, string? correlationId,
        Dictionary<string, RuntimeOverride>? assignmentOverrides,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("SERVICE: Starting workflow for definition {WorkflowDefinitionId}",
                workflowDefinitionId);

            var executionResult = await _workflowEngine.StartWorkflowAsync(
                workflowDefinitionId, instanceName, startedBy, initialVariables, correlationId, assignmentOverrides,
                cancellationToken);

            if (executionResult.Status == WorkflowExecutionStatus.Failed)
                throw new InvalidOperationException(executionResult.ErrorMessage ?? "Workflow startup failed");

            if (executionResult.WorkflowInstance == null)
                throw new InvalidOperationException("WorkflowEngine returned null instance");

            await _eventPublisher.PublishWorkflowStartedAsync(
                executionResult.WorkflowInstance.Id, workflowDefinitionId, instanceName, startedBy,
                executionResult.WorkflowInstance.StartedOn, correlationId, cancellationToken);

            _logger.LogInformation("SERVICE: Successfully started workflow instance {WorkflowInstanceId}",
                executionResult.WorkflowInstance.Id);

            return executionResult.WorkflowInstance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SERVICE: Failed to start workflow for definition {WorkflowDefinitionId}",
                workflowDefinitionId);
            throw;
        }
    }

    public async Task<WorkflowInstance> ResumeWorkflowAsync(
        Guid workflowInstanceId,
        string activityId,
        string completedBy,
        Dictionary<string, object>? input = null,
        Dictionary<string, RuntimeOverride>? nextAssignmentOverrides = null,
        CancellationToken cancellationToken = default)
    {
        // If already in a transaction (e.g., from MediatR TransactionalBehavior), run directly
        if (_unitOfWork.HasActiveTransaction)
        {
            var instance = await ExecuteResumeWorkflowAsync(workflowInstanceId, activityId, completedBy,
                input, nextAssignmentOverrides, cancellationToken);
            // Integration events deferred to post-commit in the non-transaction path
            await PublishPostCommitEventsAsync(instance, cancellationToken);
            return instance;
        }

        // Otherwise wrap in execution strategy + transaction
        WorkflowInstance? result = null;
        var strategy = _unitOfWork.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                result = await ExecuteResumeWorkflowAsync(workflowInstanceId, activityId, completedBy,
                    input, nextAssignmentOverrides, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                try { await _unitOfWork.RollbackTransactionAsync(cancellationToken); }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "SERVICE: Failed to rollback transaction");
                }
                throw;
            }
        });

        // Publish integration events AFTER transaction committed successfully
        await PublishPostCommitEventsAsync(result!, cancellationToken);

        return result!;
    }

    private async Task<WorkflowInstance> ExecuteResumeWorkflowAsync(
        Guid workflowInstanceId, string activityId, string completedBy,
        Dictionary<string, object>? input, Dictionary<string, RuntimeOverride>? nextAssignmentOverrides,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("SERVICE: Resuming workflow {WorkflowInstanceId} at activity {ActivityId}",
                workflowInstanceId, activityId);

            var executionResult = await _workflowEngine.ResumeWorkflowAsync(
                workflowInstanceId, activityId, completedBy, input, nextAssignmentOverrides, cancellationToken);

            if (executionResult.Status == WorkflowExecutionStatus.Failed)
                throw new InvalidOperationException(executionResult.ErrorMessage ?? "Workflow resume failed");

            if (executionResult.WorkflowInstance == null)
                throw new InvalidOperationException("WorkflowEngine returned null instance");

            await _eventPublisher.PublishActivityCompletedAsync(
                workflowInstanceId, activityId, completedBy, DateTime.Now, input,
                input?.TryGetValue("comments", out var commentsValue) == true ? commentsValue?.ToString() : null,
                cancellationToken);

            if (executionResult.Status == WorkflowExecutionStatus.Completed)
                await _eventPublisher.PublishWorkflowCompletedAsync(
                    workflowInstanceId, completedBy, DateTime.Now, cancellationToken);

            _logger.LogInformation("SERVICE: Successfully resumed workflow {WorkflowInstanceId} with status {Status}",
                workflowInstanceId, executionResult.Status);

            return executionResult.WorkflowInstance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SERVICE: Failed to resume workflow {WorkflowInstanceId}", workflowInstanceId);
            throw;
        }
    }

    public async Task CancelWorkflowAsync(
        Guid workflowInstanceId,
        string cancelledBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var workflowInstance =
            await _persistenceService.GetWorkflowInstanceAsync(workflowInstanceId, cancellationToken);
        if (workflowInstance == null)
            throw new InvalidOperationException($"Workflow instance not found: {workflowInstanceId}");

        workflowInstance.UpdateStatus(WorkflowStatus.Cancelled, reason);

        // Cancel any current activity execution
        var currentActivityExecution =
            await _persistenceService.GetCurrentActivityExecutionAsync(workflowInstanceId, cancellationToken);
        if (currentActivityExecution != null)
        {
            currentActivityExecution.Cancel(reason);
            await _persistenceService.UpdateActivityExecutionAsync(currentActivityExecution, cancellationToken);
        }

        // UpdateWorkflowInstanceAsync (not SaveWorkflowInstanceAsync) — the instance already
        // exists in the DB. SaveWorkflowInstanceAsync delegates to AddAsync and would cause a
        // PK collision on the WorkflowInstances row we just loaded.
        await _persistenceService.UpdateWorkflowInstanceAsync(workflowInstance, cancellationToken);

        // Archive any pending tasks tied to this instance. Without this the maker's task
        // inbox keeps showing cancelled followup tasks (e.g. ProvideAdditionalDocuments).
        // Also covers future manual cancel UIs and the parent-cancel cascade
        // (ParentWorkflowCancelledConsumer invokes this method on each child workflow).
        var pendingTasks = await _assignmentRepository.GetPendingTasksByWorkflowInstanceIdAsync(
            workflowInstanceId, cancellationToken);
        foreach (var pendingTask in pendingTasks)
        {
            var completedTask = CompletedTask.CreateFromPendingTask(
                pendingTask, "Cancelled", _dateTimeProvider.ApplicationNow);
            await _assignmentRepository.AddCompletedTaskAsync(completedTask, cancellationToken);
            await _assignmentRepository.RemovePendingTaskAsync(pendingTask, cancellationToken);
        }

        await _eventPublisher.PublishWorkflowCancelledAsync(
            workflowInstanceId,
            cancelledBy,
            DateTime.Now,
            reason,
            cancellationToken);

        _logger.LogInformation("Workflow instance {WorkflowInstanceId} cancelled by {CancelledBy}: {Reason}",
            workflowInstanceId, cancelledBy, reason);
    }

    public async Task<WorkflowInstance?> GetWorkflowInstanceAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        return await _persistenceService.GetWorkflowInstanceWithExecutionsAsync(workflowInstanceId, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetUserTasksAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _persistenceService.GetUserTasksAsync(userId, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowActivityExecution>> GetCurrentActivitiesForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _persistenceService.GetCurrentActivitiesForUserAsync(userId, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowActivityExecution>> GetCurrentActivitiesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _persistenceService.GetCurrentActivitiesAsync(cancellationToken);
    }

    public async Task<bool> ValidateWorkflowDefinitionAsync(
        WorkflowSchema workflowSchema,
        CancellationToken cancellationToken = default)
    {
        return await _workflowEngine.ValidateWorkflowDefinitionAsync(workflowSchema, cancellationToken);
    }

    /// <summary>
    /// Publishes post-commit integration events for activity landings that don't have
    /// a natural owning activity to emit from. Currently only the internal path
    /// (int-appraisal-execution) — the external path emits CompanyAssignedIntegrationEvent
    /// and InternalFollowupAssignedIntegrationEvent directly from CompanySelectionActivity
    /// and InternalFollowupSelectionActivity, which are bypassed on routeback.
    /// </summary>
    private async Task PublishPostCommitEventsAsync(WorkflowInstance instance, CancellationToken cancellationToken)
    {
        var appraisalId = WorkflowVariables.TryGetAppraisalId(instance.Variables);
        if (appraisalId is null)
        {
            _logger.LogWarning(
                "AppraisalId not yet available in Variables for workflow {WorkflowInstanceId}, skipping post-commit events",
                instance.Id);
            return;
        }

        if (instance.CurrentActivityId == "int-appraisal-execution")
        {
            PublishInternalAssignedEvent(instance, appraisalId.Value);
        }
    }

    private void PublishInternalAssignedEvent(WorkflowInstance instance, Guid correlationId)
    {
        var assigneeUserId = instance.CurrentAssignee ?? "";
        if (string.IsNullOrEmpty(assigneeUserId))
        {
            _logger.LogWarning(
                "No assignee found for int-appraisal-execution, skipping InternalAssignedIntegrationEvent for {CorrelationId}",
                correlationId);
            return;
        }

        var method = instance.Variables.TryGetValue("assignmentMethod", out var am)
            && !string.IsNullOrEmpty(am?.ToString()) ? am.ToString()! : "RoundRobin";
        var followupMethod = instance.Variables.TryGetValue("internalFollowupMethod", out var ifm)
            && !string.IsNullOrEmpty(ifm?.ToString()) ? ifm.ToString() : "RoundRobin";
        var internalStaffId = instance.Variables.TryGetValue("internalFollowupStaffId", out var ifs)
            && !string.IsNullOrEmpty(ifs?.ToString()) ? ifs.ToString() : assigneeUserId;

        var appraisalNumber = instance.Variables.TryGetValue("appraisalNumber", out var an) ? an?.ToString() : null;

        _outbox.Publish(new InternalAssignedIntegrationEvent
        {
            AppraisalId = correlationId,
            AssigneeUserId = assigneeUserId,
            InternalAppraiserId = internalStaffId,
            AssignmentMethod = method,
            InternalFollowupAssignmentMethod = followupMethod,
            CompletedBy = instance.LastCompletedBy,
            AppraisalNumber = appraisalNumber
        }, correlationId: correlationId.ToString());

        _logger.LogInformation(
            "Published InternalAssignedIntegrationEvent after commit: AppraisalId={AppraisalId}, AssigneeUserId={UserId}, Method={Method}",
            correlationId, assigneeUserId, method);
    }

}