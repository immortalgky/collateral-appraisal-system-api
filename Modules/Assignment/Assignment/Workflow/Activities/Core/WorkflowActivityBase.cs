using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assignment.Workflow.Activities.Core;

public abstract class WorkflowActivityBase : IWorkflowActivity
{
    public abstract string ActivityType { get; }
    public abstract string Name { get; }
    public virtual string Description => Name;

    public abstract Task<ActivityResult> ExecuteAsync(ActivityContext context,
        CancellationToken cancellationToken = default);

    public virtual Task<ValidationResult> ValidateAsync(ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ValidationResult.Success());
    }

    protected T GetProperty<T>(ActivityContext context, string key, T defaultValue = default!)
    {
        if (context.Properties.TryGetValue(key, out var value))
        {
            // Direct type match - fastest path
            if (value is T typedValue)
                return typedValue;

            // Handle JsonElement from frontend data
            if (value is JsonElement jsonElement)
            {
                return HandleJsonElement(jsonElement, defaultValue);
            }

            // Handle string conversion
            if (typeof(T) == typeof(string))
            {
                return (T)(object)(value.ToString() ?? string.Empty);
            }

            // Handle complex object conversion via JSON (for DTOs, dictionaries, etc.)
            if (!typeof(T).IsPrimitive && typeof(T) != typeof(string) && typeof(T) != typeof(DateTime))
            {
                return HandleComplexTypeConversion(value, defaultValue);
            }

            // Handle primitive type conversion
            if (value is IConvertible convertible)
            {
                try
                {
                    return (T)Convert.ChangeType(convertible, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
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
            {
                return jsonElement.ValueKind == JsonValueKind.String
                    ? (T)(object)(jsonElement.GetString() ?? string.Empty)
                    : (T)(object)jsonElement.GetRawText();
            }

            // Handle JSON string containing serialized object (common from frontend)
            if (jsonElement.ValueKind == JsonValueKind.String)
            {
                var jsonString = jsonElement.GetString();
                if (!string.IsNullOrEmpty(jsonString) && IsJsonString(jsonString))
                {
                    return JsonSerializer.Deserialize<T>(jsonString, jsonOptions) ?? defaultValue;
                }
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
            {
                return JsonSerializer.Deserialize<T>(jsonString, jsonOptions) ?? defaultValue;
            }

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

    protected T GetVariable<T>(ActivityContext context, string key, T defaultValue = default!)
    {
        if (context.Variables.TryGetValue(key, out var value))
        {
            // Direct type match - fastest path
            if (value is T typedValue)
                return typedValue;

            // Handle JsonElement from frontend data
            if (value is JsonElement jsonElement)
            {
                return HandleJsonElement(jsonElement, defaultValue);
            }

            // Handle string conversion
            if (typeof(T) == typeof(string))
            {
                return (T)(object)(value.ToString() ?? string.Empty);
            }

            // Handle complex object conversion via JSON (for DTOs, dictionaries, etc.)
            if (!typeof(T).IsPrimitive && typeof(T) != typeof(string) && typeof(T) != typeof(DateTime))
            {
                return HandleComplexTypeConversion(value, defaultValue);
            }

            // Handle primitive type conversion
            if (value is IConvertible convertible)
            {
                try
                {
                    return (T)Convert.ChangeType(convertible, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
        }

        return defaultValue;
    }

    protected bool EvaluateCondition(ActivityContext context, string? condition)
    {
        if (string.IsNullOrEmpty(condition))
            return true;

        // Simple condition evaluation - can be enhanced with expression engine
        // For now, support basic variable comparisons like "status == 'approved'"
        try
        {
            var parts = condition.Split(new[] { "==", "!=", ">=", "<=", ">", "<" },
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return false;

            var variable = parts[0].Trim();
            var expectedValue = parts[1].Trim().Trim('\'', '"');
            var actualValue = GetVariable<string>(context, variable);

            var op = condition.Contains("==") ? "==" :
                condition.Contains("!=") ? "!=" :
                condition.Contains(">=") ? ">=" :
                condition.Contains("<=") ? "<=" :
                condition.Contains(">") ? ">" :
                condition.Contains("<") ? "<" : "==";

            return op switch
            {
                "==" => string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase),
                _ => true // For now, default to true for unsupported operations
            };
        }
        catch
        {
            return false;
        }
    }
}