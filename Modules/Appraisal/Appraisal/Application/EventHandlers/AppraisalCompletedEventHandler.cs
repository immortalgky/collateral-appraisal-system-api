using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.EventHandlers;

public class AppraisalCompletedEventHandler(
    ILogger<AppraisalCompletedEventHandler> logger,
    IIntegrationEventOutbox outbox,
    IDateTimeProvider dateTimeProvider) : INotificationHandler<AppraisalCompletedEvent>
{
    public Task Handle(AppraisalCompletedEvent notification, CancellationToken cancellationToken)
    {
        var appraisal = notification.Appraisal;

        logger.LogInformation("Domain Event handled: {DomainEvent} for AppraisalId: {AppraisalId}",
            nameof(AppraisalCompletedEvent), appraisal.Id);

        outbox.Publish(new AppraisalCompletedIntegrationEvent
        {
            AppraisalId = appraisal.Id,
            RequestId = appraisal.RequestId,
            CompletedAt = dateTimeProvider.ApplicationNow
        }, correlationId: appraisal.Id.ToString());

        logger.LogInformation(
            "Published AppraisalCompletedIntegrationEvent for AppraisalId: {AppraisalId}, RequestId: {RequestId}",
            appraisal.Id, appraisal.RequestId);

        return Task.CompletedTask;
    }
}
