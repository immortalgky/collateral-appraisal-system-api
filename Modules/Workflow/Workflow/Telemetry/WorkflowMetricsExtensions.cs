using System.Diagnostics;
using Workflow.Workflow.Models;
using Workflow.Workflow.Engine.Core;

namespace Workflow.Telemetry;

/// <summary>
/// Extension methods for WorkflowMetrics to provide convenience methods for common operations
/// and automatic tag extraction from workflow context objects.
/// </summary>
public static class WorkflowMetricsExtensions
{
    /// <summary>
    /// Records workflow started with automatic tag extraction from workflow instance.
    /// </summary>
    public static void RecordWorkflowStarted(this IWorkflowMetrics metrics, WorkflowInstance workflowInstance)
    {
        var tags = ExtractWorkflowTags(workflowInstance);
        metrics.RecordWorkflowStarted(
            workflowInstance.WorkflowDefinition?.Name ?? "Unknown",
            workflowInstance.WorkflowDefinition?.Id.ToString() ?? "Unknown",
            tags);
    }

    /// <summary>
    /// Records workflow completed with automatic tag extraction from workflow instance.
    /// </summary>
    public static void RecordWorkflowCompleted(this IWorkflowMetrics metrics, WorkflowInstance workflowInstance)
    {
        var tags = ExtractWorkflowTags(workflowInstance);
        metrics.RecordWorkflowCompleted(
            workflowInstance.WorkflowDefinition?.Name ?? "Unknown",
            workflowInstance.WorkflowDefinition?.Id.ToString() ?? "Unknown",
            workflowInstance.Status.ToString(),
            tags);
    }

    /// <summary>
    /// Records workflow failed with automatic tag extraction and error details.
    /// </summary>
    public static void RecordWorkflowFailed(this IWorkflowMetrics metrics, WorkflowInstance workflowInstance, Exception exception)
    {
        var tags = ExtractWorkflowTags(workflowInstance)
            .Concat(ExtractExceptionTags(exception))
            .ToArray();

        metrics.RecordWorkflowFailed(
            workflowInstance.WorkflowDefinition?.Name ?? "Unknown",
            workflowInstance.WorkflowDefinition?.Id.ToString() ?? "Unknown",
            exception.GetType().Name,
            tags);
    }

    /// <summary>
    /// Records activity execution with automatic tag extraction from activity execution.
    /// </summary>
    public static void RecordActivityStarted(this IWorkflowMetrics metrics, WorkflowActivityExecution activityExecution)
    {
        var tags = ExtractActivityTags(activityExecution);
        metrics.RecordActivityStarted(
            activityExecution.ActivityType ?? "Unknown",
            activityExecution.ActivityName ?? "Unknown",
            activityExecution.WorkflowInstance?.WorkflowDefinition?.Name ?? "Unknown",
            tags);
    }

    /// <summary>
    /// Records activity completion with automatic tag extraction from activity execution.
    /// </summary>
    public static void RecordActivityCompleted(this IWorkflowMetrics metrics, WorkflowActivityExecution activityExecution)
    {
        var tags = ExtractActivityTags(activityExecution);
        metrics.RecordActivityCompleted(
            activityExecution.ActivityType ?? "Unknown",
            activityExecution.ActivityName ?? "Unknown",
            activityExecution.WorkflowInstance?.WorkflowDefinition?.Name ?? "Unknown",
            activityExecution.Status.ToString(),
            tags);
    }

    /// <summary>
    /// Records activity duration with automatic tag extraction and timing calculation.
    /// </summary>
    public static void RecordActivityDuration(this IWorkflowMetrics metrics, WorkflowActivityExecution activityExecution)
    {
        if (activityExecution.CompletedOn.HasValue)
        {
            var duration = activityExecution.CompletedOn.Value - activityExecution.StartedOn;
            var tags = ExtractActivityTags(activityExecution);
            
            metrics.RecordActivityDuration(
                activityExecution.ActivityType ?? "Unknown",
                activityExecution.ActivityName ?? "Unknown",
                activityExecution.WorkflowInstance?.WorkflowDefinition?.Name ?? "Unknown",
                duration,
                activityExecution.Status.ToString(),
                tags);
        }
    }

    /// <summary>
    /// Records workflow duration with automatic timing calculation from workflow instance.
    /// </summary>
    public static void RecordWorkflowDuration(this IWorkflowMetrics metrics, WorkflowInstance workflowInstance)
    {
        if (workflowInstance.CompletedOn.HasValue)
        {
            var duration = workflowInstance.CompletedOn.Value - workflowInstance.StartedOn;
            var tags = ExtractWorkflowTags(workflowInstance);
            
            metrics.RecordWorkflowDuration(
                workflowInstance.WorkflowDefinition?.Name ?? "Unknown",
                workflowInstance.WorkflowDefinition?.Id.ToString() ?? "Unknown",
                duration,
                workflowInstance.Status.ToString(),
                tags);
        }
    }

    /// <summary>
    /// Creates a timing scope that automatically records duration when disposed.
    /// Usage: using (metrics.CreateTimingScope(...)) { /* timed operation */ }
    /// </summary>
    public static IDisposable CreateWorkflowTimingScope(
        this IWorkflowMetrics metrics, 
        string workflowType, 
        string workflowDefinitionId,
        string status = "completed",
        params KeyValuePair<string, object?>[] tags)
    {
        return new WorkflowTimingScope(metrics, workflowType, workflowDefinitionId, status, tags);
    }

    /// <summary>
    /// Creates a timing scope for activity execution that automatically records duration when disposed.
    /// </summary>
    public static IDisposable CreateActivityTimingScope(
        this IWorkflowMetrics metrics,
        string activityType,
        string activityName,
        string workflowType,
        string status = "completed",
        params KeyValuePair<string, object?>[] tags)
    {
        return new ActivityTimingScope(metrics, activityType, activityName, workflowType, status, tags);
    }

    /// <summary>
    /// Records external call with HTTP response details.
    /// </summary>
    public static void RecordExternalCall(this IWorkflowMetrics metrics, HttpResponseMessage response, string url, string method, TimeSpan duration)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("success", response.IsSuccessStatusCode),
            new KeyValuePair<string, object?>("response_size", response.Content.Headers.ContentLength ?? 0)
        };

        metrics.RecordExternalCall(url, method, (int)response.StatusCode, duration, tags);
    }

    /// <summary>
    /// Records workflow retry with automatic error extraction.
    /// </summary>
    public static void RecordWorkflowRetry(this IWorkflowMetrics metrics, WorkflowInstance workflowInstance, Exception exception, int retryCount)
    {
        var tags = ExtractWorkflowTags(workflowInstance)
            .Concat(ExtractExceptionTags(exception))
            .ToArray();

        metrics.RecordWorkflowRetry(
            workflowInstance.WorkflowDefinition?.Name ?? "Unknown",
            exception.GetType().Name,
            retryCount,
            tags);
    }

    /// <summary>
    /// Extracts common workflow tags from a workflow instance.
    /// </summary>
    private static KeyValuePair<string, object?>[] ExtractWorkflowTags(WorkflowInstance workflowInstance)
    {
        return new[]
        {
            new KeyValuePair<string, object?>(WorkflowTelemetryConstants.SemanticAttributes.WorkflowInstanceId, workflowInstance.Id.ToString()),
            new KeyValuePair<string, object?>(WorkflowTelemetryConstants.SemanticAttributes.WorkflowInstanceCorrelationId, workflowInstance.CorrelationId),
            new KeyValuePair<string, object?>(WorkflowTelemetryConstants.SemanticAttributes.WorkflowInstanceStatus, workflowInstance.Status.ToString()),
            new KeyValuePair<string, object?>("version", workflowInstance.WorkflowDefinition?.Version.ToString() ?? "1"),
            new KeyValuePair<string, object?>("environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development")
        };
    }

    /// <summary>
    /// Extracts common activity tags from a workflow activity execution.
    /// </summary>
    private static KeyValuePair<string, object?>[] ExtractActivityTags(WorkflowActivityExecution activityExecution)
    {
        return new[]
        {
            new KeyValuePair<string, object?>(WorkflowTelemetryConstants.SemanticAttributes.WorkflowActivityExecutionId, activityExecution.Id.ToString()),
            new KeyValuePair<string, object?>(WorkflowTelemetryConstants.SemanticAttributes.WorkflowActivityId, activityExecution.ActivityId),
            new KeyValuePair<string, object?>(WorkflowTelemetryConstants.SemanticAttributes.WorkflowInstanceId, activityExecution.WorkflowInstanceId.ToString()),
            new KeyValuePair<string, object?>("environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development")
        };
    }

    /// <summary>
    /// Extracts exception-related tags for error tracking.
    /// </summary>
    private static KeyValuePair<string, object?>[] ExtractExceptionTags(Exception exception)
    {
        return new[]
        {
            new KeyValuePair<string, object?>(WorkflowTelemetryConstants.SemanticAttributes.WorkflowErrorType, exception.GetType().Name),
            new KeyValuePair<string, object?>(WorkflowTelemetryConstants.SemanticAttributes.WorkflowErrorMessage, exception.Message),
            new KeyValuePair<string, object?>("error_source", exception.Source ?? "Unknown"),
            new KeyValuePair<string, object?>("has_inner_exception", exception.InnerException != null)
        };
    }

    /// <summary>
    /// Timing scope for workflow operations that automatically records duration.
    /// </summary>
    private class WorkflowTimingScope : IDisposable
    {
        private readonly IWorkflowMetrics _metrics;
        private readonly string _workflowType;
        private readonly string _workflowDefinitionId;
        private readonly string _status;
        private readonly KeyValuePair<string, object?>[] _tags;
        private readonly Stopwatch _stopwatch;

        public WorkflowTimingScope(IWorkflowMetrics metrics, string workflowType, string workflowDefinitionId, string status, KeyValuePair<string, object?>[] tags)
        {
            _metrics = metrics;
            _workflowType = workflowType;
            _workflowDefinitionId = workflowDefinitionId;
            _status = status;
            _tags = tags;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _metrics.RecordWorkflowDuration(_workflowType, _workflowDefinitionId, _stopwatch.Elapsed, _status, _tags);
        }
    }

    /// <summary>
    /// Timing scope for activity operations that automatically records duration.
    /// </summary>
    private class ActivityTimingScope : IDisposable
    {
        private readonly IWorkflowMetrics _metrics;
        private readonly string _activityType;
        private readonly string _activityName;
        private readonly string _workflowType;
        private readonly string _status;
        private readonly KeyValuePair<string, object?>[] _tags;
        private readonly Stopwatch _stopwatch;

        public ActivityTimingScope(IWorkflowMetrics metrics, string activityType, string activityName, string workflowType, string status, KeyValuePair<string, object?>[] tags)
        {
            _metrics = metrics;
            _activityType = activityType;
            _activityName = activityName;
            _workflowType = workflowType;
            _status = status;
            _tags = tags;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _metrics.RecordActivityDuration(_activityType, _activityName, _workflowType, _stopwatch.Elapsed, _status, _tags);
        }
    }
}