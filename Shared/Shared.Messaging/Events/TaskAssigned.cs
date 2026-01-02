using Shared.Messaging.Values;

namespace Shared.Messaging.Events;

public record TaskAssigned
{
    public Guid CorrelationId { get; init; }
    public TaskName TaskName { get; init; } = default!;
    public string AssignedTo { get; init; } = default!;
    public string AssignedType { get; init; } = default!;
}