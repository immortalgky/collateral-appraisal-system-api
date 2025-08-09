namespace Assignment.Workflow.Activities.Core;

public abstract class WorkflowActivityBase : IWorkflowActivity
{
    public abstract string ActivityType { get; }
    public abstract string Name { get; }
    public virtual string Description => Name;

    public abstract Task<ActivityResult> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken = default);

    public virtual Task<ValidationResult> ValidateAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ValidationResult.Success());
    }

    protected T GetProperty<T>(ActivityContext context, string key, T defaultValue = default!)
    {
        if (context.Properties.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
                return typedValue;
                
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    protected T GetVariable<T>(ActivityContext context, string key, T defaultValue = default!)
    {
        if (context.Variables.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
                return typedValue;
                
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
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
            var parts = condition.Split(new[] { "==", "!=", ">=", "<=", ">", "<" }, StringSplitOptions.RemoveEmptyEntries);
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