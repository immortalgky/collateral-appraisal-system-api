using System.Text.Json.Serialization;
using Workflow.Workflow.Engine.Expression;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Activities.Core;

public abstract class WorkflowActivityBase : IWorkflowActivity
{
    private readonly ExpressionEvaluator _expressionEvaluator = new();

    public abstract string ActivityType { get; }
    public abstract string Name { get; }
    public virtual string Description => Name;

    public virtual async Task<ActivityResult> ExecuteAsync(ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        WorkflowActivityExecution? execution = null;

        try
        {
            // Activity creates and manages its own execution
            execution = CreateActivityExecution(context);

            // Add execution to a workflow instance and set current activity
            context.WorkflowInstance.AddActivityExecution(execution);
            context.WorkflowInstance.SetCurrentActivity(context.ActivityId);

            // Start execution tracking
            execution.Start();

            // Call the derived class implementation
            var result = await OnExecuteAsync(context, cancellationToken);

            // Update execution based on a result
            if (result.Status == ActivityResultStatus.Completed)
                execution.Complete("system", result.OutputData, result.Comments);
            else if (result.Status == ActivityResultStatus.Failed)
                execution.Fail(result.ErrorMessage ?? "Activity execution failed");
            // For Pending status, execution remains InProgress

            return result;
        }
        catch (Exception ex)
        {
            execution?.Fail(ex.Message);
            return ActivityResult.Failed(ex.Message);
        }
    }

    protected abstract Task<ActivityResult> OnExecuteAsync(ActivityContext context,
        CancellationToken cancellationToken = default);

    public virtual async Task<ActivityResult> ResumeAsync(ActivityContext context,
        Dictionary<string, object> resumeInput,
        CancellationToken cancellationToken = default)
    {
        // Find the in-progress execution for this activity
        var execution = FindActivityExecution(context);
        if (execution == null)
            return ActivityResult.Failed($"No in-progress execution found for activity {context.ActivityId}");

        try
        {
            // Call the derived class implementation or use the default behavior
            var result = await OnResumeAsync(context, resumeInput, cancellationToken);

            // Update execution based on a result
            if (result.Status == ActivityResultStatus.Completed)
                execution.Complete(GetCompletedBy(resumeInput), result.OutputData, result.Comments);
            else if (result.Status == ActivityResultStatus.Failed)
                execution.Fail(result.ErrorMessage ?? "Activity resume failed");

            return result;
        }
        catch (Exception ex)
        {
            execution.Fail(ex.Message);
            return ActivityResult.Failed(ex.Message);
        }
    }

    protected virtual Task<ActivityResult> OnResumeAsync(ActivityContext context,
        Dictionary<string, object> resumeInput,
        CancellationToken cancellationToken = default)
    {
        // Default implementation: transform resume input into workflow variables
        // Activities can override this to implement custom input/output mapping
        var outputData = new Dictionary<string, object>();

        // Map common fields with an activity prefix to avoid conflicts
        if (resumeInput.TryGetValue("decisionTaken", out var decision))
        {
            outputData[$"{NormalizeActivityId(context.ActivityId)}_decisionTaken"] = decision;
            outputData["decision"] = decision; // Also keep for transition evaluation
        }

        return Task.FromResult(ActivityResult.Success(outputData));
    }

    public virtual Task<ValidationResult> ValidateAsync(ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ValidationResult.Success());
    }

    // Helper methods for execution management

    protected virtual WorkflowActivityExecution CreateActivityExecution(ActivityContext context)
    {
        return WorkflowActivityExecution.Create(
            context.WorkflowInstance.Id,
            context.ActivityId,
            Name,
            ActivityType,
            null,
            context.Variables);
    }

    protected virtual WorkflowActivityExecution? FindActivityExecution(ActivityContext context)
    {
        return context.WorkflowInstance.ActivityExecutions
            .FirstOrDefault(ae =>
                ae.ActivityId == context.ActivityId && ae.Status == ActivityExecutionStatus.InProgress);
    }

    protected virtual void SetActivityAssignee(ActivityContext context, string? assigneeId)
    {
        if (!string.IsNullOrEmpty(assigneeId))
            // Update current activity with assignee
            context.WorkflowInstance.SetCurrentActivity(context.ActivityId, assigneeId);
    }

    private string GetCompletedBy(Dictionary<string, object> resumeInput)
    {
        if (resumeInput.TryGetValue("completedBy", out var completedBy)) return completedBy.ToString() ?? "system";

        return "system";
    }

    protected T GetProperty<T>(ActivityContext context, string key, T defaultValue = default!)
    {
        return GetValue(context.Properties, key, defaultValue);
    }

    protected T GetVariable<T>(ActivityContext context, string key, T defaultValue = default!)
    {
        return GetValue(context.Variables, key, defaultValue);
    }

    private T GetValue<T>(IDictionary<string, object> source, string key, T defaultValue = default!)
    {
        if (source.TryGetValue(key, out var value))
        {
            // Direct type match - fastest path
            if (value is T typedValue)
                return typedValue;

            // Handle JsonElement from frontend data
            if (value is JsonElement jsonElement) return HandleJsonElement(jsonElement, defaultValue);

            // Handle string conversion
            if (typeof(T) == typeof(string)) return (T)(object)(value.ToString() ?? string.Empty);

            // Handle complex object conversion via JSON (for DTOs, dictionaries, etc.)
            if (!typeof(T).IsPrimitive && typeof(T) != typeof(string) && typeof(T) != typeof(DateTime))
                return HandleComplexTypeConversion(value, defaultValue);

            // Handle primitive type conversion
            if (value is IConvertible convertible)
                try
                {
                    return (T)Convert.ChangeType(convertible, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
        }

        return defaultValue;
    }

    private T HandleJsonElement<T>(JsonElement jsonElement, T defaultValue)
    {
        // Frontend JSON serializer options for better compatibility
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        try
        {
            // Handle string target type
            if (typeof(T) == typeof(string))
                return jsonElement.ValueKind == JsonValueKind.String
                    ? (T)(object)(jsonElement.GetString() ?? string.Empty)
                    : (T)(object)jsonElement.GetRawText();

            // Handle JSON string containing a serialized object (common from frontend)
            if (jsonElement.ValueKind == JsonValueKind.String)
            {
                var jsonString = jsonElement.GetString();
                if (!string.IsNullOrEmpty(jsonString) && IsJsonString(jsonString))
                    return JsonSerializer.Deserialize<T>(jsonString, jsonOptions) ?? defaultValue;
            }

            // Handle direct JSON object deserialization
            return jsonElement.Deserialize<T>(jsonOptions) ?? defaultValue;
        }
        catch (JsonException)
        {
            return defaultValue;
        }
    }

    private T HandleComplexTypeConversion<T>(object value, T defaultValue)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        try
        {
            // If value is already a JSON string, deserialize directly
            if (value is string jsonString && IsJsonString(jsonString))
                return JsonSerializer.Deserialize<T>(jsonString, jsonOptions) ?? defaultValue;

            // Otherwise, serialize then deserialize (for object mapping)
            var json = JsonSerializer.Serialize(value, jsonOptions);
            return JsonSerializer.Deserialize<T>(json, jsonOptions) ?? defaultValue;
        }
        catch (JsonException)
        {
            return defaultValue;
        }
    }

    private static bool IsJsonString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        return (trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
               (trimmed.StartsWith("[") && trimmed.EndsWith("]"));
    }

    protected bool EvaluateCondition(ActivityContext context, string? condition)
    {
        if (string.IsNullOrEmpty(condition))
            return true;

        try
        {
            return _expressionEvaluator.EvaluateExpression(condition, context.Variables);
        }
        catch
        {
            return false;
        }
    }

    protected bool ValidateExpression(string expression, out string? errorMessage)
    {
        return _expressionEvaluator.ValidateExpression(expression, out errorMessage);
    }

    protected string NormalizeActivityId(string activityId)
    {
        return activityId.Replace("-", "_");
    }
}