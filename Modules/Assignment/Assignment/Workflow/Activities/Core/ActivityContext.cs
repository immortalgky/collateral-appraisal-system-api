using Assignment.Workflow.Models;

namespace Assignment.Workflow.Activities.Core;

public class ActivityContext
{
    public Guid WorkflowInstanceId { get; init; }
    public string ActivityId { get; init; } = default!;
    public Dictionary<string, object> Properties { get; init; } = new();
    public Dictionary<string, object> Variables { get; init; } = new();
    public Dictionary<string, object> InputData { get; init; } = new();
    public string? CurrentAssignee { get; init; }
    public CancellationToken CancellationToken { get; init; }
    public WorkflowInstance WorkflowInstance { get; init; } = default!;
    
    /// <summary>
    /// Runtime assignment overrides provided via API calls
    /// These take highest priority in assignment logic
    /// </summary>
    public RuntimeOverride? RuntimeOverrides { get; init; }
}