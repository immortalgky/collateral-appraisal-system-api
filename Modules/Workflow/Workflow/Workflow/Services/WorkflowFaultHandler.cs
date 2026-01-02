using Microsoft.EntityFrameworkCore;
using Workflow.Data;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;

namespace Workflow.Workflow.Services;

public class WorkflowFaultHandler : IWorkflowFaultHandler
{
    private readonly WorkflowDbContext _context;
    private readonly IWorkflowExecutionLogRepository _executionLogRepository;
    private readonly IWorkflowInstanceRepository _workflowRepository;
    private readonly IWorkflowOutboxRepository _outboxRepository;
    private readonly ILogger<WorkflowFaultHandler> _logger;

    // Fault thresholds
    private const int MaxRetryAttempts = 3;
    private const int SuspensionThreshold = 5; // Suspend after 5 consecutive failures
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(30);

    public WorkflowFaultHandler(
        WorkflowDbContext context,
        IWorkflowExecutionLogRepository executionLogRepository,
        IWorkflowInstanceRepository workflowRepository,
        IWorkflowOutboxRepository outboxRepository,
        ILogger<WorkflowFaultHandler> logger)
    {
        _context = context;
        _executionLogRepository = executionLogRepository;
        _workflowRepository = workflowRepository;
        _outboxRepository = outboxRepository;
        _logger = logger;
    }

    public async Task<FaultHandlingResult> HandleWorkflowStartupFaultAsync(
        StartWorkflowFaultContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(context.Exception, 
            "Workflow startup fault for definition {WorkflowDefinitionId}, attempt {AttemptNumber}",
            context.WorkflowDefinitionId, context.AttemptNumber);

        // Log the fault
        var faultLog = WorkflowExecutionLog.Create(
            Guid.NewGuid(), // No workflow instance yet
            ExecutionLogEvent.WorkflowFailed,
            errorMessage: context.Exception.Message,
            actorId: context.StartedBy,
            metadata: new Dictionary<string, object>
            {
                ["workflowDefinitionId"] = context.WorkflowDefinitionId,
                ["attemptNumber"] = context.AttemptNumber,
                ["faultType"] = "startup"
            });

        await _executionLogRepository.AddAsync(faultLog, cancellationToken);

        // Determine retry strategy based on exception type and attempt number
        var shouldRetry = ShouldRetryStartupFailure(context.Exception, context.AttemptNumber);
        var retryDelay = CalculateRetryDelay(context.AttemptNumber);

        return new FaultHandlingResult(
            ShouldRetry: shouldRetry,
            SuspendWorkflow: false, // Can't suspend what doesn't exist yet
            RequiresManualIntervention: !shouldRetry && context.AttemptNumber >= MaxRetryAttempts,
            RetryDelay: shouldRetry ? retryDelay : null,
            RecommendedAction: GetRecommendedAction(context.Exception)
        );
    }

    public async Task<FaultHandlingResult> HandleActivityExecutionFaultAsync(
        ActivityFaultContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(context.Exception,
            "Activity execution fault for workflow {WorkflowInstanceId}, activity {ActivityId}, attempt {AttemptNumber}",
            context.WorkflowInstanceId, context.ActivityId, context.AttemptNumber);

        // Log the activity fault
        var faultLog = WorkflowExecutionLog.ActivityFailed(
            context.WorkflowInstanceId,
            context.ActivityId,
            context.Exception.Message,
            metadata: new Dictionary<string, object>
            {
                ["attemptNumber"] = context.AttemptNumber,
                ["activityType"] = context.ActivityType,
                ["faultType"] = "activity-execution"
            });

        await _executionLogRepository.AddAsync(faultLog, cancellationToken);

        // Check if workflow should be suspended
        var shouldSuspend = await ShouldSuspendWorkflowAsync(
            context.WorkflowInstanceId, "activity-fault", cancellationToken);

        if (shouldSuspend)
        {
            await SuspendWorkflowAsync(context.WorkflowInstanceId, 
                $"Too many activity failures: {context.Exception.Message}", cancellationToken);
        }

        var shouldRetry = ShouldRetryActivityFailure(context.Exception, context.AttemptNumber);
        var requiresManualIntervention = IsManualInterventionRequired(context.Exception);

        return new FaultHandlingResult(
            ShouldRetry: shouldRetry && !shouldSuspend,
            SuspendWorkflow: shouldSuspend,
            RequiresManualIntervention: requiresManualIntervention || shouldSuspend,
            RetryDelay: shouldRetry ? CalculateRetryDelay(context.AttemptNumber) : null,
            RecommendedAction: GetRecommendedAction(context.Exception)
        );
    }

    public async Task<FaultHandlingResult> HandleExternalCallFaultAsync(
        ExternalCallFaultContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(context.Exception,
            "External call fault for call {ExternalCallId}, workflow {WorkflowInstanceId}, attempt {AttemptNumber}",
            context.ExternalCallId, context.WorkflowInstanceId, context.AttemptNumber);

        // Log the external call fault
        var faultLog = WorkflowExecutionLog.Create(
            context.WorkflowInstanceId,
            ExecutionLogEvent.ExternalCallFailed,
            context.ActivityId,
            $"External call failed: {context.Exception.Message}",
            metadata: new Dictionary<string, object>
            {
                ["externalCallId"] = context.ExternalCallId,
                ["callType"] = context.CallType.ToString(),
                ["endpoint"] = context.Endpoint,
                ["attemptNumber"] = context.AttemptNumber
            });

        await _executionLogRepository.AddAsync(faultLog, cancellationToken);

        var shouldRetry = ShouldRetryExternalCall(context.Exception, context.AttemptNumber);
        var retryDelay = CalculateExponentialBackoff(context.AttemptNumber);

        return new FaultHandlingResult(
            ShouldRetry: shouldRetry,
            SuspendWorkflow: false, // External calls don't suspend workflows
            RequiresManualIntervention: !shouldRetry,
            RetryDelay: shouldRetry ? retryDelay : null,
            RecommendedAction: $"Review external service: {context.Endpoint}"
        );
    }

    public async Task<FaultHandlingResult> HandleWorkflowResumeFaultAsync(
        WorkflowResumeFaultContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(context.Exception,
            "Workflow resume fault for workflow {WorkflowInstanceId}, activity {ActivityId}, attempt {AttemptNumber}",
            context.WorkflowInstanceId, context.ActivityId, context.AttemptNumber);

        // Log the resume fault
        var faultLog = WorkflowExecutionLog.Create(
            context.WorkflowInstanceId,
            ExecutionLogEvent.WorkflowFailed,
            context.ActivityId,
            $"Resume failed: {context.Exception.Message}",
            metadata: new Dictionary<string, object>
            {
                ["bookmarkKey"] = context.BookmarkKey,
                ["attemptNumber"] = context.AttemptNumber,
                ["faultType"] = "resume"
            });

        await _executionLogRepository.AddAsync(faultLog, cancellationToken);

        var shouldRetry = context.AttemptNumber < MaxRetryAttempts && 
                         !IsNonRetryableException(context.Exception);

        return new FaultHandlingResult(
            ShouldRetry: shouldRetry,
            SuspendWorkflow: false,
            RequiresManualIntervention: !shouldRetry,
            RetryDelay: shouldRetry ? CalculateRetryDelay(context.AttemptNumber) : null,
            RecommendedAction: "Check workflow state and bookmark validity"
        );
    }

    public async Task<bool> ShouldSuspendWorkflowAsync(
        Guid workflowInstanceId,
        string errorType,
        CancellationToken cancellationToken = default)
    {
        var recentFailures = await _executionLogRepository.GetByEventTypeAsync(
            ExecutionLogEvent.ActivityFailed,
            DateTime.UtcNow.Subtract(FailureWindow),
            DateTime.UtcNow,
            cancellationToken);

        var workflowFailures = recentFailures
            .Where(log => log.WorkflowInstanceId == workflowInstanceId)
            .Count();

        return workflowFailures >= SuspensionThreshold;
    }

    public async Task<CompensationPlan> CreateCompensationPlanAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _workflowRepository.GetWithExecutionsAsync(workflowInstanceId, cancellationToken);
        if (workflow == null)
        {
            throw new InvalidOperationException($"Workflow {workflowInstanceId} not found");
        }

        var completedActivities = workflow.ActivityExecutions
            .Where(ae => ae.Status == ActivityExecutionStatus.Completed)
            .OrderByDescending(ae => ae.CompletedOn)
            .ToList();

        var compensationSteps = new List<CompensationStep>();

        foreach (var activity in completedActivities)
        {
            // Create compensation steps based on activity type
            var step = CreateCompensationStep(activity);
            if (step != null)
            {
                compensationSteps.Add(step);
            }
        }

        var strategy = DetermineCompensationStrategy(workflow, compensationSteps);

        return new CompensationPlan(
            workflowInstanceId,
            compensationSteps,
            strategy
        );
    }

    private bool ShouldRetryStartupFailure(Exception exception, int attemptNumber)
    {
        if (attemptNumber >= MaxRetryAttempts)
            return false;

        return exception switch
        {
            InvalidOperationException => false, // Workflow definition issues
            ArgumentException => false,         // Invalid parameters
            UnauthorizedAccessException => false, // Permission issues
            TimeoutException => true,           // Network/DB timeouts
            DbUpdateException => true,          // Database concurrency
            _ => true                          // Default to retry
        };
    }

    private bool ShouldRetryActivityFailure(Exception exception, int attemptNumber)
    {
        if (attemptNumber >= MaxRetryAttempts)
            return false;

        return !IsNonRetryableException(exception);
    }

    private bool ShouldRetryExternalCall(Exception exception, int attemptNumber)
    {
        if (attemptNumber >= 5) // More retries for external calls
            return false;

        return exception switch
        {
            HttpRequestException => true,
            TimeoutException => true,
            TaskCanceledException => true,
            _ => false
        };
    }

    private bool IsNonRetryableException(Exception exception)
    {
        return exception switch
        {
            ArgumentException => true,
            InvalidOperationException => true,
            UnauthorizedAccessException => true,
            NotSupportedException => true,
            _ => false
        };
    }

    private bool IsManualInterventionRequired(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException => true,
            ArgumentException => true,
            InvalidOperationException => true,
            _ => false
        };
    }

    private TimeSpan CalculateRetryDelay(int attemptNumber)
    {
        // Exponential backoff: 1s, 2s, 4s, 8s, 16s
        var delaySeconds = Math.Pow(2, attemptNumber - 1);
        return TimeSpan.FromSeconds(Math.Min(delaySeconds, 30)); // Max 30 seconds
    }

    private TimeSpan CalculateExponentialBackoff(int attemptNumber)
    {
        // Longer delays for external calls: 5s, 10s, 20s, 40s, 60s
        var delaySeconds = 5 * Math.Pow(2, attemptNumber - 1);
        return TimeSpan.FromSeconds(Math.Min(delaySeconds, 60)); // Max 60 seconds
    }

    private string GetRecommendedAction(Exception exception)
    {
        return exception switch
        {
            TimeoutException => "Check database connectivity and performance",
            HttpRequestException => "Verify external service availability",
            UnauthorizedAccessException => "Check user permissions and authentication",
            ArgumentException => "Validate input parameters and workflow definition",
            InvalidOperationException => "Review workflow state and business logic",
            _ => "Review error logs and contact support if needed"
        };
    }

    private async Task SuspendWorkflowAsync(
        Guid workflowInstanceId,
        string reason,
        CancellationToken cancellationToken)
    {
        var workflow = await _workflowRepository.GetForUpdateAsync(workflowInstanceId, cancellationToken);
        if (workflow != null)
        {
            workflow.UpdateStatus(WorkflowStatus.Suspended, reason);
            await _workflowRepository.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Suspended workflow {WorkflowInstanceId}: {Reason}",
                workflowInstanceId, reason);
        }
    }

    private CompensationStep? CreateCompensationStep(WorkflowActivityExecution activity)
    {
        // This would be customized based on activity types
        return activity.ActivityType switch
        {
            "EmailActivity" => new CompensationStep(
                $"undo-{activity.Id}",
                $"Send cancellation email for {activity.ActivityName}",
                CompensationAction.ReverseExternalCall,
                new Dictionary<string, object> { ["originalActivityId"] = activity.Id },
                false),
            
            "DataUpdateActivity" => new CompensationStep(
                $"rollback-{activity.Id}",
                $"Rollback data changes from {activity.ActivityName}",
                CompensationAction.UndoActivity,
                new Dictionary<string, object> { ["originalActivityId"] = activity.Id },
                true),
            
            _ => new CompensationStep(
                $"log-{activity.Id}",
                $"Log completion of {activity.ActivityName}",
                CompensationAction.LogError,
                new Dictionary<string, object> { ["originalActivityId"] = activity.Id },
                false)
        };
    }

    private CompensationStrategy DetermineCompensationStrategy(
        WorkflowInstance workflow,
        List<CompensationStep> steps)
    {
        // Simple strategy determination - in reality this would be more sophisticated
        if (steps.Any(s => s.IsRequired))
        {
            return CompensationStrategy.Rollback;
        }

        return workflow.Status switch
        {
            WorkflowStatus.Failed => CompensationStrategy.ManualIntervention,
            WorkflowStatus.Suspended => CompensationStrategy.Forward,
            _ => CompensationStrategy.Ignore
        };
    }
}