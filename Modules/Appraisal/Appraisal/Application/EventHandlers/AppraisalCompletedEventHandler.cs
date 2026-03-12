using MassTransit;
using Shared.Messaging.Events;

namespace Appraisal.Application.EventHandlers;

public class AppraisalCompletedEventHandler(
    ILogger<AppraisalCompletedEventHandler> logger,
    IBus bus) : INotificationHandler<AppraisalCompletedEvent>
{
    public async Task Handle(AppraisalCompletedEvent notification, CancellationToken cancellationToken)
    {
        var appraisal = notification.Appraisal;

        logger.LogInformation("Domain Event handled: {DomainEvent} for AppraisalId: {AppraisalId}",
            nameof(AppraisalCompletedEvent), appraisal.Id);

        var integrationEvent = new AppraisalCompletedIntegrationEvent
        {
            RequestId = appraisal.RequestId,
            CompletedAt = DateTime.UtcNow
        };

        await bus.Publish(integrationEvent, cancellationToken);

        logger.LogInformation(
            "Published AppraisalCompletedIntegrationEvent for AppraisalId: {AppraisalId}, RequestId: {RequestId}",
            appraisal.Id, appraisal.RequestId);
    }
}
