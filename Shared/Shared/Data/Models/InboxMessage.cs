using Shared.DDD;

namespace Shared.Data.Models;

public class InboxMessage : Entity<Guid>
{
    public DateTime OccurredOn { get; private set; } = default!;
    public string EventType { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public DateTime? ReceiveAt { get; private set; } = DateTime.Now;

    private InboxMessage() { }

    private InboxMessage(Guid id, DateTime occurredOn, string eventType, string payload)
    {
        Id = id;
        OccurredOn = occurredOn;
        EventType = eventType;
        Payload = payload;
    }

    public static InboxMessage Create(Guid id, DateTime occurredOn, string eventType, string payload)
    {
        return new InboxMessage(id, occurredOn, eventType, payload);
    }

}