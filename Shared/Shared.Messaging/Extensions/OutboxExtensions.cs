using System.Text.Json;
using MassTransit;

namespace Shared.Messaging.Extensions;

public static class OutboxExtensions
{
    /// <summary>
    /// Deserialize event payload and publish through MassTransit
    /// </summary>
    public static async Task PublishDeserializedEvent(
        this IPublishEndpoint publishEndpoint,
        string jsonPayload,
        string eventTypeString,
        CancellationToken cancellationToken
        )
    {
        // EventType string to Type with simple registry lookup
        var eventType = Type.GetType(eventTypeString)//GetTypeFromAllAssemblies(eventTypeString) 
            ?? throw new InvalidOperationException($"Cannot resolve event type: {eventTypeString}");

        // Deserialize JSON payload to event object
        var eventObject = JsonSerializer.Deserialize(jsonPayload, eventType) ?? throw new InvalidOperationException($"Failed to deserialize event payload for type: {eventTypeString}");

        await publishEndpoint.Publish(eventObject, eventType, cancellationToken);
    }
}
