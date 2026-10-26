namespace Shared.Messaging.Events;

/// <summary>
/// A pool task (AssignedType "2") was completed and left its pool. Delivered via the outbox so it is
/// only seen after the completing transaction commits; the Workflow consumer turns it into a SignalR
/// push so the other pool members refresh their list.
/// </summary>
public record PoolTaskCompletedIntegrationEvent : IntegrationEvent
{
    public string PoolGroup { get; init; } = default!;
    public Guid TaskId { get; init; }
    public string CompletedBy { get; init; } = default!;
}
