using Shared.Messaging.Values;

namespace Shared.Messaging.Events;

public record TaskCompleted
{
    public Guid CorrelationId { get; init; }
    public TaskName TaskName { get; init; } = default!;
    public string ActionTaken { get; init; } = default!;
}