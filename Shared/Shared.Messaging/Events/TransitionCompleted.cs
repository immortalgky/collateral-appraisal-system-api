using Shared.Messaging.Values;

namespace Shared.Messaging.Events;

public record TransitionCompleted
{
    public Guid CorrelationId { get; init; } = Guid.Empty!;
    public long RequestId { get; init; }
    public TaskName TaskName { get; init; } = default!;
    public string CurrentState { get; init; } = default!;
    public string AssignedTo { get; init; } = default!;
    public string AssignedType { get; init; } = default!;
}