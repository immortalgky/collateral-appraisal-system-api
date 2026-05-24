namespace Shared.Data.Outbox;

public class InboxMessage
{
    public Guid MessageId { get; private set; }
    public string ConsumerType { get; private set; } = default!;
    public InboxMessageStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    private InboxMessage() { }

    public static InboxMessage Create(Guid messageId, string consumerType, DateTime startedAt)
    {
        return new InboxMessage
        {
            MessageId = messageId,
            ConsumerType = consumerType,
            Status = InboxMessageStatus.Processing,
            StartedAt = startedAt
        };
    }

    public void MarkAsProcessed(DateTime processedAt)
    {
        Status = InboxMessageStatus.Processed;
        ProcessedAt = processedAt;
    }
}

public enum InboxMessageStatus
{
    Processing,
    Processed
}
