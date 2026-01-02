using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;

namespace Workflow.Workflow.Engine.Core;

/// <summary>
/// Encapsulates workflow execution context to reduce parameter passing and provide centralized access
/// to workflow execution state and metadata
/// </summary>
public class WorkflowExecutionContext
{
    public WorkflowSchema Schema { get; }
    public WorkflowInstance WorkflowInstance { get; }

    /// <summary>
    /// Execution metadata and tracking information
    /// </summary>
    public WorkflowExecutionMetadata Metadata { get; }

    public WorkflowExecutionContext(
        WorkflowSchema schema,
        WorkflowInstance workflowInstance,
        WorkflowExecutionMetadata? metadata = null)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        WorkflowInstance = workflowInstance ?? throw new ArgumentNullException(nameof(workflowInstance));
        Metadata = metadata ?? new WorkflowExecutionMetadata();
    }

    /// <summary>
    /// Creates activity context for the current workflow execution
    /// </summary>
    public ActivityContext CreateActivityContext(ActivityDefinition activityDefinition)
    {
        // Extract runtime override for this specific activity (if any)
        RuntimeOverride? activityRuntimeOverride = null;
        if (WorkflowInstance.RuntimeOverrides.TryGetValue(activityDefinition.Id, out var runtimeOverride))
            activityRuntimeOverride = runtimeOverride;

        return new ActivityContext
        {
            WorkflowInstanceId = WorkflowInstance.Id,
            ActivityId = activityDefinition.Id,
            Properties = activityDefinition.Properties,
            Variables = WorkflowInstance.Variables,
            InputData = new Dictionary<string, object>(),
            CurrentAssignee = WorkflowInstance.CurrentAssignee,
            WorkflowInstance = WorkflowInstance,
            RuntimeOverrides = activityRuntimeOverride
        };
    }

    /// <summary>
    /// Finds activity definition by ID
    /// </summary>
    public ActivityDefinition? FindActivityById(string activityId)
    {
        return Schema.Activities.FirstOrDefault(a => a.Id == activityId);
    }

    /// <summary>
    /// Gets current activity definition
    /// </summary>
    public ActivityDefinition? GetCurrentActivity()
    {
        if (string.IsNullOrEmpty(WorkflowInstance.CurrentActivityId))
            return null;

        return FindActivityById(WorkflowInstance.CurrentActivityId);
    }

    /// <summary>
    /// Checks if the workflow is in a valid state for execution
    /// </summary>
    public bool IsExecutable =>
        WorkflowInstance.Status != WorkflowStatus.Failed &&
        WorkflowInstance.Status != WorkflowStatus.Completed;

    /// <summary>
    /// Gets workflow variables as read-only dictionary
    /// </summary>
    public IReadOnlyDictionary<string, object> Variables =>
        WorkflowInstance.Variables.AsReadOnly();

    /// <summary>
    /// Gets runtime overrides as a read-only dictionary
    /// </summary>
    public IReadOnlyDictionary<string, RuntimeOverride> RuntimeOverrides =>
        WorkflowInstance.RuntimeOverrides.AsReadOnly();

    /// <summary>
    /// Creates a new context with an updated workflow instance
    /// </summary>
    public WorkflowExecutionContext WithUpdatedInstance(WorkflowInstance updatedInstance)
    {
        return new WorkflowExecutionContext(Schema, updatedInstance, Metadata);
    }

    /// <summary>
    /// Creates a new context with updated metadata
    /// </summary>
    public WorkflowExecutionContext WithMetadata(WorkflowExecutionMetadata metadata)
    {
        return new WorkflowExecutionContext(Schema, WorkflowInstance, metadata);
    }

    /// <summary>
    /// Adds an execution step to metadata for tracking
    /// </summary>
    public void TrackExecutionStep(string activityId, ActivityResultStatus status, TimeSpan duration)
    {
        Metadata.ExecutionSteps.Add(new ExecutionStep
        {
            ActivityId = activityId,
            Status = status,
            Duration = duration,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Gets total execution time from metadata
    /// </summary>
    public TimeSpan TotalExecutionTime =>
        Metadata.ExecutionSteps.Any()
            ? Metadata.ExecutionSteps.Sum(s => s.Duration.TotalMilliseconds)
                .Let(total => TimeSpan.FromMilliseconds(total))
            : TimeSpan.Zero;

    /// <summary>
    /// Gets count of failed activities from metadata
    /// </summary>
    public int FailedActivitiesCount =>
        Metadata.ExecutionSteps.Count(s => s.Status == ActivityResultStatus.Failed);
}

/// <summary>
/// Metadata for tracking workflow execution progress and performance
/// </summary>
public class WorkflowExecutionMetadata
{
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public List<ExecutionStep> ExecutionSteps { get; set; } = new();
    public Dictionary<string, object> AdditionalData { get; set; } = new();

    /// <summary>
    /// Adds custom metadata
    /// </summary>
    public void AddMetadata(string key, object value)
    {
        AdditionalData[key] = value;
    }

    /// <summary>
    /// Gets custom metadata
    /// </summary>
    public T? GetMetadata<T>(string key)
    {
        if (AdditionalData.TryGetValue(key, out var value) && value is T typedValue)
            return typedValue;
        return default;
    }
}

/// <summary>
/// Represents a single execution step in the workflow
/// </summary>
public class ExecutionStep
{
    public string ActivityId { get; set; } = string.Empty;
    public ActivityResultStatus Status { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime Timestamp { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> OutputData { get; set; } = new();
}

/// <summary>
/// Extension methods for workflow execution context
/// </summary>
public static class WorkflowExecutionContextExtensions
{
    /// <summary>
    /// Extension method to apply a function and return result (functional helper)
    /// </summary>
    public static TResult Let<T, TResult>(this T value, Func<T, TResult> func)
    {
        return func(value);
    }
}