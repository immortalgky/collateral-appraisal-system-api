using System.Text.Json;

namespace Shared.Data.Outbox;

public interface IIntegrationEventOutbox
{
    void Publish<TEvent>(TEvent @event, string? correlationId = null, Dictionary<string, string>? headers = null)
        where TEvent : class;
}

public class IntegrationEventOutbox(IOutboxScope outboxScope) : IIntegrationEventOutbox
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Publish<TEvent>(TEvent @event, string? correlationId = null, Dictionary<string, string>? headers = null)
        where TEvent : class
    {
        // Store type name without version/culture/token for resilience across deployments
        var eventType = $"{typeof(TEvent).FullName}, {typeof(TEvent).Assembly.GetName().Name}";
        var payload = JsonSerializer.Serialize(@event, SerializerOptions);

        var message = IntegrationEventOutboxMessage.Create(eventType, payload, correlationId, headers);
        outboxScope.Add(message);
    }
}
