using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Appraisal.Application.EventHandlers;

public class AppraisalStatusChangedEventHandler(
    ILogger<AppraisalStatusChangedEventHandler> logger,
    IIntegrationEventOutbox outbox) : INotificationHandler<AppraisalStatusChangedEvent>
{
    public Task Handle(AppraisalStatusChangedEvent notification, CancellationToken cancellationToken)
    {
        var appraisal = notification.Appraisal;

        logger.LogInformation("Domain Event handled: {DomainEvent} for AppraisalId: {AppraisalId} {OldStatus} → {NewStatus}",
            nameof(AppraisalStatusChangedEvent), appraisal.Id,
            notification.OldStatus?.Code ?? "(none)",
            notification.NewStatus.Code);

        outbox.Publish(new AppraisalStatusChangedIntegrationEvent
        {
            AppraisalId = appraisal.Id,
            AppraisalNumber = appraisal.AppraisalNumber,
            PreviousStatus = notification.OldStatus?.Code,
            Status = notification.NewStatus.Code,
        }, correlationId: appraisal.Id.ToString());

        return Task.CompletedTask;
    }
}
