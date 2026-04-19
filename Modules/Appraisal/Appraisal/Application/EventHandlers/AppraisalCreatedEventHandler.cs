using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.EventHandlers;

public class AppraisalCreatedEventHandler(
    ILogger<AppraisalCreatedEventHandler> logger,
    IIntegrationEventOutbox outbox,
    IDateTimeProvider dateTimeProvider) : INotificationHandler<AppraisalCreatedEvent>
{
    public Task Handle(AppraisalCreatedEvent notification, CancellationToken cancellationToken)
    {
        var appraisal = notification.Appraisal;

        logger.LogInformation("Domain Event handled: {DomainEvent} for AppraisalId: {AppraisalId}",
            nameof(AppraisalCreatedEvent), appraisal.Id);

        outbox.Publish(new AppraisalCreatedIntegrationEvent
        {
            AppraisalId = appraisal.Id,
            RequestId = appraisal.RequestId,
            AppraisalNumber = appraisal.AppraisalNumber,
            AppraisalType = appraisal.AppraisalType,
            CreatedBy = notification.RequestedBy ?? appraisal.CreatedBy,
            RequestedBy = notification.RequestedBy,
            CreatedAt = dateTimeProvider.ApplicationNow,
            IsPma = appraisal.IsPma,
            FacilityLimit = appraisal.FacilityLimit,
            Priority = appraisal.Priority,
            HasAppraisalBook = appraisal.HasAppraisalBook,
            Channel = appraisal.Channel
        }, appraisal.Id.ToString());

        logger.LogInformation(
            "Published AppraisalCreatedIntegrationEvent for AppraisalId: {AppraisalId}, RequestId: {RequestId}",
            appraisal.Id, appraisal.RequestId);

        return Task.CompletedTask;
    }
}