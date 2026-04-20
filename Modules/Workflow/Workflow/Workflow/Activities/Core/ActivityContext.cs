using Workflow.Workflow.Models;

namespace Workflow.Workflow.Activities.Core;

public class ActivityContext
{
    public Guid WorkflowInstanceId { get; init; }
    public string ActivityId { get; init; } = default!;
    public string ActivityName { get; init; } = default!;
    public Dictionary<string, object> Properties { get; init; } = new();
    public Dictionary<string, object> Variables { get; init; } = new();
    public Dictionary<string, object> InputData { get; init; } = new();
    public string? CurrentAssignee { get; init; }
    public CancellationToken CancellationToken { get; init; }
    public WorkflowInstance WorkflowInstance { get; init; } = default!;
    public RuntimeOverride? RuntimeOverrides { get; init; }

    /// <summary>
    /// Movement stamp inherited from the previous completed activity execution.
    /// "F" = Forward (default), "B" = Backward (route-back). The engine sets this when it
    /// builds the context; activities pass it through to PendingTask.Movement via the
    /// TaskAssigned / ApprovalTasksAssigned events.
    /// </summary>
    public string Movement { get; init; } = "F";
}