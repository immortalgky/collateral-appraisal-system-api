namespace Shared.Messaging.Events;

public record TaskCompleted
{
    public Guid CorrelationId { get; init; }
    public string TaskName { get; init; } = default!;
    public string ActionTaken { get; init; } = default!;
}