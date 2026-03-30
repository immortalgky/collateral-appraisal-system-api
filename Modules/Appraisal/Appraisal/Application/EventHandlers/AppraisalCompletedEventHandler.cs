using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Appraisal.Application.EventHandlers;

public class AppraisalCompletedEventHandler(
    ILogger<AppraisalCompletedEventHandler> logger,
    IIntegrationEventOutbox outbox) : INotificationHandler<AppraisalCompletedEvent>
{
    public Task Handle(AppraisalCompletedEvent notification, CancellationToken cancellationToken)
    {
        var appraisal = notification.Appraisal;

        logger.LogInformation("Domain Event handled: {DomainEvent} for AppraisalId: {AppraisalId}",
            nameof(AppraisalCompletedEvent), appraisal.Id);

        outbox.Publish(new AppraisalCompletedIntegrationEvent
        {
            RequestId = appraisal.RequestId,
            CompletedAt = DateTime.UtcNow
        }, correlationId: appraisal.Id.ToString());

        logger.LogInformation(
            "Published AppraisalCompletedIntegrationEvent for AppraisalId: {AppraisalId}, RequestId: {RequestId}",
            appraisal.Id, appraisal.RequestId);

        return Task.CompletedTask;
    }
}
