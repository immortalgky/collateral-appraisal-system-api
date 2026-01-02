using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Workflow.Telemetry;

/// <summary>
/// Implementation of workflow metrics using System.Diagnostics.Metrics API.
/// Follows OpenTelemetry semantic conventions and provides comprehensive observability
/// for workflow operations, activities, and system health.
/// </summary>
public class WorkflowMetrics : IWorkflowMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly ILogger<WorkflowMetrics> _logger;

    // Counters for workflow lifecycle events
    private readonly Counter<long> _workflowsStartedCounter;
    private readonly Counter<long> _workflowsCompletedCounter;
    private readonly Counter<long> _workflowsFailedCounter;
    private readonly Counter<long> _workflowsSuspendedCounter;
    private readonly Counter<long> _workflowsResumedCounter;
    private readonly Counter<long> _workflowsCancelledCounter;
    private readonly Counter<long> _workflowRetriesCounter;

    // Counters for activity lifecycle events
    private readonly Counter<long> _activitiesStartedCounter;
    private readonly Counter<long> _activitiesCompletedCounter;
    
    // Counters for bookmark operations
    private readonly Counter<long> _bookmarkOperationsCounter;
    
    // Counters for external operations
    private readonly Counter<long> _externalCallsCounter;

    // Histograms for duration measurements
    private readonly Histogram<double> _workflowDurationHistogram;
    private readonly Histogram<double> _activityDurationHistogram;
    private readonly Histogram<double> _bookmarkProcessingDurationHistogram;
    private readonly Histogram<double> _externalCallDurationHistogram;

    // Up-down counters (gauges) for current state metrics
    private readonly UpDownCounter<int> _activeWorkflowsGauge;
    private readonly UpDownCounter<int> _pendingActivitiesGauge;
    private readonly UpDownCounter<int> _suspendedWorkflowsGauge;

    public WorkflowMetrics(ILogger<WorkflowMetrics> logger)
    {
        _logger = logger;
        _meter = new Meter(WorkflowTelemetryConstants.MeterName, WorkflowTelemetryConstants.Version);

        // Initialize counters with OpenTelemetry semantic conventions
        _workflowsStartedCounter = _meter.CreateCounter<long>(
            "workflows_started_total",
            "1",
            "Total number of workflows started");

        _workflowsCompletedCounter = _meter.CreateCounter<long>(
            "workflows_completed_total",
            "1",
            "Total number of workflows completed successfully");

        _workflowsFailedCounter = _meter.CreateCounter<long>(
            "workflows_failed_total",
            "1",
            "Total number of workflows that failed");

        _workflowsSuspendedCounter = _meter.CreateCounter<long>(
            "workflows_suspended_total",
            "1",
            "Total number of workflows suspended");

        _workflowsResumedCounter = _meter.CreateCounter<long>(
            "workflows_resumed_total",
            "1",
            "Total number of workflows resumed");

        _workflowsCancelledCounter = _meter.CreateCounter<long>(
            "workflows_cancelled_total",
            "1",
            "Total number of workflows cancelled");

        _workflowRetriesCounter = _meter.CreateCounter<long>(
            "workflow_retries_total",
            "1",
            "Total number of workflow retry attempts");

        _activitiesStartedCounter = _meter.CreateCounter<long>(
            "workflow_activities_started_total",
            "1",
            "Total number of workflow activities started");

        _activitiesCompletedCounter = _meter.CreateCounter<long>(
            "workflow_activities_completed_total",
            "1",
            "Total number of workflow activities completed");

        _bookmarkOperationsCounter = _meter.CreateCounter<long>(
            "workflow_bookmark_operations_total",
            "1",
            "Total number of workflow bookmark operations");

        _externalCallsCounter = _meter.CreateCounter<long>(
            "workflow_external_calls_total",
            "1",
            "Total number of external calls made by workflows");

        // Initialize histograms for duration measurements
        _workflowDurationHistogram = _meter.CreateHistogram<double>(
            "workflow_execution_duration_seconds",
            "s",
            "Duration of workflow executions in seconds");

        _activityDurationHistogram = _meter.CreateHistogram<double>(
            "workflow_activity_execution_duration_seconds",
            "s",
            "Duration of workflow activity executions in seconds");

        _bookmarkProcessingDurationHistogram = _meter.CreateHistogram<double>(
            "workflow_bookmark_processing_duration_seconds",
            "s",
            "Duration of workflow bookmark processing operations in seconds");

        _externalCallDurationHistogram = _meter.CreateHistogram<double>(
            "workflow_external_call_duration_seconds",
            "s",
            "Duration of external calls made by workflows in seconds");

        // Initialize gauges for current state metrics
        _activeWorkflowsGauge = _meter.CreateUpDownCounter<int>(
            "workflow_active_instances_current",
            "1",
            "Current number of active workflow instances");

        _pendingActivitiesGauge = _meter.CreateUpDownCounter<int>(
            "workflow_pending_activities_current",
            "1",
            "Current number of pending workflow activities");

        _suspendedWorkflowsGauge = _meter.CreateUpDownCounter<int>(
            "workflow_suspended_instances_current",
            "1",
            "Current number of suspended workflow instances");

        _logger.LogDebug("WorkflowMetrics initialized with meter: {MeterName} version: {Version}",
            WorkflowTelemetryConstants.MeterName, WorkflowTelemetryConstants.Version);
    }

    public void RecordWorkflowStarted(string workflowType, string workflowDefinitionId, params KeyValuePair<string, object?>[] tags)
    {
        var allTags = CreateTags(
            (WorkflowTelemetryConstants.SemanticAttributes.WorkflowDefinitionId, workflowDefinitionId),
            ("workflow_type", workflowType))
            .Concat(tags);

        _workflowsStartedCounter.Add(1, allTags.ToArray());

        _logger.LogTrace("Recorded workflow started metric: WorkflowType={WorkflowType}, DefinitionId={DefinitionId}",
            workflowType, workflowDefinitionId);
    }

    public void RecordWorkflowCompleted(string workflowType, string workflowDefinitionId, string status, params KeyValuePair<string, object?>[] tags)
    {
        var allTags = CreateTags(
            (WorkflowTelemetryConstants.SemanticAttributes.WorkflowDefinitionId, workflowDefinitionId),
            ("workflow_type", workflowType),
            ("status", status))
            .Concat(tags);

        _workflowsCompletedCounter.Add(1, allTags.ToArray());

        _logger.LogTrace("Recorded workflow completed metric: WorkflowType={WorkflowType}, DefinitionId={DefinitionId}, Status={Status}",
            workflowType, workflowDefinitionId, status);
    }

    public void RecordWorkflowFailed(string workflowType, string workflowDefinitionId, string errorType, params KeyValuePair<string, object?>[] tags)
    {
        var allTags = CreateTags(
            (WorkflowTelemetryConstants.SemanticAttributes.WorkflowDefinitionId, workflowDefinitionId),
            ("workflow_type", workflowType),
            (WorkflowTelemetryConstants.SemanticAttributes.WorkflowErrorType, errorType))
            .Concat(tags);

        _workflowsFailedCounter.Add(1, allTags.ToArray());

        _logger.LogTrace("Recorded workflow failed metric: WorkflowType={WorkflowType}, DefinitionId={DefinitionId}, ErrorType={ErrorType}",
            workflowType, workflowDefinitionId, errorType);
    }

    public void RecordWorkflowSuspended(string workflowType, string workflowDefinitionId, string reason, params KeyValuePair<string, object?>[] tags)
    {
        var allTags = CreateTags(
            (WorkflowTelemetryConstants.SemanticAttributes.WorkflowDefinitionId, workflowDefinitionId),
            ("workflow_type", workflowType),
            ("suspend_reason", reason))
            .Concat(tags);

        _workflowsSuspendedCounter.Add(1, allTags.ToArray());

        _logger.LogTrace("Recorded workflow suspended metric: WorkflowType={WorkflowType}, DefinitionId={DefinitionId}, Reason={Reason}",
            workflowType, workflowDefinitionId, reason);
    }

    public void RecordWorkflowResumed(string workflowType, string workflowDefinitionId, params KeyValuePair<string, object?>[] tags)
    {
        var allTags = CreateTags(
            (WorkflowTelemetryConstants.SemanticAttributes.WorkflowDefinitionId, workflowDefinitionId),
            ("workflow_type", workflowType))
            .Concat(tags);

        _workflowsResumedCounter.Add(1, allTags.ToArray());

        _logger.LogTrace("Recorded workflow resumed metric: WorkflowType={WorkflowType}, DefinitionId={DefinitionId}",
            workflowType, workflowDefinitionId);
    }

    public void RecordWorkflowCancelled(string workflowType, string workflowDefinitionId, string reason, params KeyValuePair<string, object?>[] tags)
    {
        var allTags = CreateTags(
            (WorkflowTelemetryConstants.SemanticAttributes.WorkflowDefinitionId, workflowDefinitionId),
            ("workflow_type", workflowType),
            ("cancel_reason", reason))
            .Concat(tags);

        _workflowsCancelledCounter.Add(1, allTags.ToArray());

        _logger.LogTrace("Recorded workflow cancelled metric: WorkflowType={WorkflowType}, DefinitionId={DefinitionId}, Reason={Reason}",
            workflowType, workflowDefinitionId, reason);
    }

    public void RecordWorkflowDuration(string workflowType, string workflowDefinitionId, TimeSpan duration, string status, params KeyValuePair<string, object?>[] tags)
    {
        var allTags = CreateTags(
            (WorkflowTelemetryConstants.SemanticAttributes.WorkflowDefinitionId, workflowDefinitionId),
            ("workflow_type", workflowType),
            ("status", status))
            .Concat(tags);

        _workflowDurationHistogram.Record(duration.TotalSeconds, allTags.ToArray());

        _logger.LogTrace("Recorded workflow duration metric: WorkflowType={WorkflowType}, DefinitionId={DefinitionId}, Duration={Duration}ms, Status={Status}",
            workflowType, workflowDefinitionId, duration.TotalMilliseconds, status);
    }

    public void RecordActivityStarted(string activityType, string activityName, string workflowType, params KeyValuePair<string, object?>[] tags)
    {
        var allTags = CreateTags(
            (WorkflowTelemetryConstants.SemanticAttributes.WorkflowActivityType, activityType),
            (WorkflowTelemetryConstants.SemanticAttributes.WorkflowActivityName, activityName),
            ("workflow_type", workflowType))
            .Concat(tags);

        _activitiesStartedCounter.Add(1, allTags.ToArray());

        _logger.LogTrace("Recorded activity started metric: ActivityType={ActivityType}, ActivityName={ActivityName}, WorkflowType={WorkflowType}",
            activityType, activityName, workflowType);
    }

    public void RecordActivityCompleted(string activityType, string activityName, string workflowType, string status, params KeyValuePair<string, object?>[] tags)
    {
        var allTags = CreateTags(
            (WorkflowTelemetryConstants.SemanticAttributes.WorkflowActivityType, activityType),
            (WorkflowTelemetryConstants.SemanticAttributes.WorkflowActivityName, activityName),
            ("workflow_type", workflowType),
            ("status", status))
            .Concat(tags);

        _activitiesCompletedCounter.Add(1, allTags.ToArray());

        _logger.LogTrace("Recorded activity completed metric: ActivityType={ActivityType}, ActivityName={ActivityName}, WorkflowType={WorkflowType}, Status={Status}",
            activityType, activityName, workflowType, status);
    }

    public void RecordActivityDuration(string activityType, string activityName, string workflowType, TimeSpan duration, string status, params KeyValuePair<string, object?>[] tags)
    {
        var allTags = CreateTags(
            (WorkflowTelemetryConstants.SemanticAttributes.WorkflowActivityType, activityType),
            (WorkflowTelemetryConstants.SemanticAttributes.WorkflowActivityName, activityName),
            ("workflow_type", workflowType),
            ("status", status))
            .Concat(tags);

        _activityDurationHistogram.Record(duration.TotalSeconds, allTags.ToArray());

        _logger.LogTrace("Recorded activity duration metric: ActivityType={ActivityType}, ActivityName={ActivityName}, WorkflowType={WorkflowType}, Duration={Duration}ms, Status={Status}",
            activityType, activityName, workflowType, duration.TotalMilliseconds, status);
    }

    public void RecordBookmarkOperation(string operation, string bookmarkName, string workflowType, TimeSpan duration, params KeyValuePair<string, object?>[] tags)
    {
        var allTags = CreateTags(
            ("operation", operation),
            (WorkflowTelemetryConstants.SemanticAttributes.WorkflowBookmarkName, bookmarkName),
            ("workflow_type", workflowType))
            .Concat(tags);

        _bookmarkOperationsCounter.Add(1, allTags.ToArray());
        _bookmarkProcessingDurationHistogram.Record(duration.TotalSeconds, allTags.ToArray());

        _logger.LogTrace("Recorded bookmark operation metric: Operation={Operation}, BookmarkName={BookmarkName}, WorkflowType={WorkflowType}, Duration={Duration}ms",
            operation, bookmarkName, workflowType, duration.TotalMilliseconds);
    }

    public void UpdateActiveWorkflowsCount(int count, string? workflowType = null, params KeyValuePair<string, object?>[] tags)
    {
        var allTags = workflowType != null 
            ? CreateTags(("workflow_type", workflowType)).Concat(tags)
            : tags.AsEnumerable();

        // Note: UpDownCounter represents absolute values, so we need to track the delta
        // This is a simplified approach - in production, you might want to maintain state
        _activeWorkflowsGauge.Add(count, allTags.ToArray());

        _logger.LogTrace("Updated active workflows count metric: Count={Count}, WorkflowType={WorkflowType}",
            count, workflowType ?? "all");
    }

    public void UpdatePendingActivitiesCount(int count, string? activityType = null, params KeyValuePair<string, object?>[] tags)
    {
        var allTags = activityType != null 
            ? CreateTags(("activity_type", activityType)).Concat(tags)
            : tags.AsEnumerable();

        _pendingActivitiesGauge.Add(count, allTags.ToArray());

        _logger.LogTrace("Updated pending activities count metric: Count={Count}, ActivityType={ActivityType}",
            count, activityType ?? "all");
    }

    public void UpdateSuspendedWorkflowsCount(int count, string? workflowType = null, params KeyValuePair<string, object?>[] tags)
    {
        var allTags = workflowType != null 
            ? CreateTags(("workflow_type", workflowType)).Concat(tags)
            : tags.AsEnumerable();

        _suspendedWorkflowsGauge.Add(count, allTags.ToArray());

        _logger.LogTrace("Updated suspended workflows count metric: Count={Count}, WorkflowType={WorkflowType}",
            count, workflowType ?? "all");
    }

    public void RecordExternalCall(string url, string method, int statusCode, TimeSpan duration, params KeyValuePair<string, object?>[] tags)
    {
        var allTags = CreateTags(
            (WorkflowTelemetryConstants.SemanticAttributes.ExternalCallUrl, url),
            (WorkflowTelemetryConstants.SemanticAttributes.ExternalCallMethod, method),
            (WorkflowTelemetryConstants.SemanticAttributes.ExternalCallStatusCode, statusCode))
            .Concat(tags);

        _externalCallsCounter.Add(1, allTags.ToArray());
        _externalCallDurationHistogram.Record(duration.TotalSeconds, allTags.ToArray());

        _logger.LogTrace("Recorded external call metric: Url={Url}, Method={Method}, StatusCode={StatusCode}, Duration={Duration}ms",
            url, method, statusCode, duration.TotalMilliseconds);
    }

    public void RecordWorkflowRetry(string workflowType, string retryReason, int retryCount, params KeyValuePair<string, object?>[] tags)
    {
        var allTags = CreateTags(
            ("workflow_type", workflowType),
            ("retry_reason", retryReason),
            ("retry_count", retryCount))
            .Concat(tags);

        _workflowRetriesCounter.Add(1, allTags.ToArray());

        _logger.LogTrace("Recorded workflow retry metric: WorkflowType={WorkflowType}, RetryReason={RetryReason}, RetryCount={RetryCount}",
            workflowType, retryReason, retryCount);
    }

    /// <summary>
    /// Helper method to create tags from tuples for better readability.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, object?>> CreateTags(params (string Key, object? Value)[] tags)
    {
        return tags.Select(tag => new KeyValuePair<string, object?>(tag.Key, tag.Value));
    }

    public void Dispose()
    {
        _meter?.Dispose();
        _logger.LogDebug("WorkflowMetrics disposed");
    }
}