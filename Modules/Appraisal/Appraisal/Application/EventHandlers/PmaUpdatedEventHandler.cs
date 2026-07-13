using Microsoft.Extensions.Logging;
using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Stages <see cref="AppraisalPmaUpdatedIntegrationEvent"/> onto the transactional outbox so it
/// commits atomically with the PMA save (Pattern: RequestCreatedEventHandler). The actual PMA data
/// is looked up at delivery time by the Integration module — this event carries only identifiers.
/// </summary>
public class PmaUpdatedEventHandler(ILogger<PmaUpdatedEventHandler> logger, IIntegrationEventOutbox outbox)
    : INotificationHandler<PmaUpdatedEvent>
{
    public Task Handle(PmaUpdatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain Event handled: {DomainEvent}", notification.GetType().Name);

        outbox.Publish(new AppraisalPmaUpdatedIntegrationEvent
        {
            AppraisalId = notification.AppraisalId,
            PropertyId = notification.PropertyId
        }, correlationId: notification.AppraisalId.ToString());

        return Task.CompletedTask;
    }
}
