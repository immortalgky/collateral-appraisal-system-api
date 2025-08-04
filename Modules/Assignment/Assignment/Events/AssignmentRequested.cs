namespace Assignment.Events;

public record AssignmentRequested
{
    public Guid CorrelationId { get; init; }
    public string TaskName { get; init; } = default!;
}