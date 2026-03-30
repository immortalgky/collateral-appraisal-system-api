namespace Shared.Data.Outbox;

public class OutboxDeliveryLock
{
    public string Id { get; private set; } = default!;
    public string InstanceId { get; private set; } = default!;
    public DateTime LeasedUntil { get; private set; }
    public DateTime AcquiredAt { get; private set; }

    private OutboxDeliveryLock() { }
}
