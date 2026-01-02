using Shared.Messaging.Values;

namespace Assignment.Domain.Events;

public record AssignmentRequested
{
    public Guid CorrelationId { get; init; }
    public TaskName TaskName { get; init; } = default!;
}