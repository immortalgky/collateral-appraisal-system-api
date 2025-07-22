namespace Assignment.Events;

public record TransitionCompleted
{
    public Guid CorrelationId { get; init; } = Guid.Empty!;
    public long RequestId { get; init; }
    public string TaskName { get; init; } = default!;
    public string AssignedTo { get; init; } = default!;
    public string AssignedType { get; init; } = default!;
}