namespace Shared.Data.Outbox;

public interface IOutboxScope
{
    void Add(IntegrationEventOutboxMessage message);
    IReadOnlyList<IntegrationEventOutboxMessage> Messages { get; }
    void Clear();
}

public class OutboxScope : IOutboxScope
{
    private readonly List<IntegrationEventOutboxMessage> _messages = new();

    public IReadOnlyList<IntegrationEventOutboxMessage> Messages => _messages;

    public void Add(IntegrationEventOutboxMessage message) => _messages.Add(message);

    public void Clear() => _messages.Clear();
}
