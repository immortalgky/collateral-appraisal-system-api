using System.Text.Json;
using Shared.Time;

namespace Shared.Data.Outbox;

public interface IIntegrationEventOutbox
{
    void Publish<TEvent>(TEvent @event, string? correlationId = null, Dictionary<string, string>? headers = null)
        where TEvent : class;
}

public class IntegrationEventOutbox(IOutboxScope outboxScope, IDateTimeProvider dateTimeProvider) : IIntegrationEventOutbox
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Publish<TEvent>(TEvent @event, string? correlationId = null, Dictionary<string, string>? headers = null)
        where TEvent : class
    {
        var now = dateTimeProvider.ApplicationNow;

        // Stamp OccurredOn at the publish boundary so every integration event gets the
        // ApplicationNow clock regardless of how/when it was constructed.
        if (@event is IHasOccurredOn stamped && stamped.OccurredOn == default)
            stamped.OccurredOn = now;

        // Store type name without version/culture/token for resilience across deployments
        var eventType = $"{typeof(TEvent).FullName}, {typeof(TEvent).Assembly.GetName().Name}";
        var payload = JsonSerializer.Serialize(@event, SerializerOptions);

        var message = IntegrationEventOutboxMessage.Create(eventType, payload, now, correlationId, headers);
        outboxScope.Add(message);
    }
}
