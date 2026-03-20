using MassTransit;
using Shared.Messaging.Events;

namespace Appraisal.Application.EventHandlers;

public class AppraisalCreatedEventHandler(
    ILogger<AppraisalCreatedEventHandler> logger,
    IBus bus) : INotificationHandler<AppraisalCreatedEvent>
{
    public async Task Handle(AppraisalCreatedEvent notification, CancellationToken cancellationToken)
    {
        var appraisal = notification.Appraisal;

        logger.LogInformation("Domain Event handled: {DomainEvent} for AppraisalId: {AppraisalId}",
            nameof(AppraisalCreatedEvent), appraisal.Id);

        var integrationEvent = new AppraisalCreatedIntegrationEvent
        {
            AppraisalId = appraisal.Id,
            RequestId = appraisal.RequestId,
            AppraisalNumber = appraisal.AppraisalNumber,
            AppraisalType = appraisal.AppraisalType,
            CreatedBy = notification.RequestedBy ?? appraisal.CreatedBy,
            CreatedAt = DateTime.UtcNow
        };

        await bus.Publish(integrationEvent, cancellationToken);

        logger.LogInformation(
            "Published AppraisalCreatedIntegrationEvent for AppraisalId: {AppraisalId}, RequestId: {RequestId}",
            appraisal.Id, appraisal.RequestId);
    }
}
