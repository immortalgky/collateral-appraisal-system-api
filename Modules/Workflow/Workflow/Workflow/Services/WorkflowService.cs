using Shared.Data.Outbox;
using Shared.Messaging.Events;
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
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(
        IWorkflowEngine workflowEngine,
        IWorkflowPersistenceService persistenceService,
        IWorkflowEventPublisher eventPublisher,
        IWorkflowUnitOfWork unitOfWork,
        IIntegrationEventOutbox outbox,
        ILogger<WorkflowService> logger)
    {
        _workflowEngine = workflowEngine;
        _persistenceService = persistenceService;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
        _outbox = outbox;
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

        await _persistenceService.SaveWorkflowInstanceAsync(workflowInstance, cancellationToken);

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
    /// Publishes integration events AFTER the transaction has committed.
    /// Only publishes CompanyAssignedIntegrationEvent when workflow lands on ext-admin with a company assigned.
    /// </summary>
    private async Task PublishPostCommitEventsAsync(WorkflowInstance instance, CancellationToken cancellationToken)
    {
        if (instance.CurrentActivityId != "ext-appraisal-assignment") return;

        if (instance.Variables.TryGetValue("assignedCompanyId", out var cid)
            && Guid.TryParse(cid?.ToString(), out var companyId))
        {
            var companyName = instance.Variables.TryGetValue("assignedCompanyName", out var cn)
                ? cn?.ToString() ?? "" : "";
            var method = instance.Variables.TryGetValue("assignmentMethod", out var am)
                ? am?.ToString() ?? "Manual" : "Manual";

            var correlationId = !string.IsNullOrEmpty(instance.CorrelationId)
                && Guid.TryParse(instance.CorrelationId, out var parsed) ? parsed : instance.Id;

            _outbox.Publish(new CompanyAssignedIntegrationEvent
            {
                AppraisalId = correlationId,
                CompanyId = companyId,
                CompanyName = companyName,
                AssignmentMethod = method
            }, correlationId: correlationId.ToString());

            _logger.LogInformation(
                "Published CompanyAssignedIntegrationEvent after commit: AppraisalId={AppraisalId}, CompanyId={CompanyId}",
                correlationId, companyId);
        }
    }
}