using Assignment.Workflow.Activities.Core;

namespace Assignment.Workflow.Activities;

public interface IWorkflowActivityFactory
{
    IWorkflowActivity CreateActivity(string activityType);
    IEnumerable<string> GetAvailableActivityTypes();
    ActivityTypeDefinition GetActivityTypeDefinition(string activityType);
}

public class ActivityTypeDefinition
{
    public string Type { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string Category { get; init; } = default!;
    public List<ActivityPropertyDefinition> Properties { get; init; } = new();
    public string Icon { get; init; } = default!;
    public string Color { get; init; } = "#3b82f6";
}

public class ActivityPropertyDefinition
{
    public string Name { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public string Type { get; init; } = default!; // string, number, boolean, array, object
    public bool Required { get; init; }
    public string? DefaultValue { get; init; }
    public string? Description { get; init; }
    public List<string>? Options { get; init; } // For select/enum types
}