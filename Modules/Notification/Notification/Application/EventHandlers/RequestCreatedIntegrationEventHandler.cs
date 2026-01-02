using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Notification.Domain.Notifications.EventHandlers;

public class RequestCreatedIntegrationEventHandler(ILogger<RequestCreatedIntegrationEventHandler> logger)
    : IConsumer<RequestCreatedIntegrationEvent>
{
    public Task Consume(ConsumeContext<RequestCreatedIntegrationEvent> context)
    {
        logger.LogInformation("Integration Event handled: {IntegrationEvent}", context.Message.GetType().Name);

        // TODO: Implement the logic to handle the RequestCreatedIntegrationEvent

        return Task.CompletedTask;
    }
}