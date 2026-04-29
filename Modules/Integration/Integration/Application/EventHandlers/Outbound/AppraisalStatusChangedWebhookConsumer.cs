using Integration.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Integration.Application.EventHandlers.Outbound;

public class AppraisalStatusChangedWebhookConsumer(
    IWebhookService webhookService,
    IAppraisalLookupService appraisalLookup,
    ILogger<AppraisalStatusChangedWebhookConsumer> logger)
    : IConsumer<AppraisalActivityTransitionedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalActivityTransitionedIntegrationEvent> context)
    {
        var msg = context.Message;

        var status = IntegrationStatusMap.Map(msg.ActivityId);
        if (status is null)
        {
            // Activity not mapped to an external status; no webhook needed.
            return;
        }

        var keys = await appraisalLookup.GetKeysAsync(msg.AppraisalId, context.CancellationToken);
        if (keys is null)
        {
            logger.LogWarning("AppraisalStatusChangedWebhookConsumer: keys not found for AppraisalId {AppraisalId}, skipping", msg.AppraisalId);
            return;
        }

        if (string.IsNullOrEmpty(keys.AppraisalNumber))
        {
            logger.LogWarning("AppraisalStatusChangedWebhookConsumer: AppraisalNumber is null for AppraisalId {AppraisalId}, skipping", msg.AppraisalId);
            return;
        }

        if (string.IsNullOrEmpty(keys.ExternalCaseKey))
        {
            logger.LogWarning("AppraisalStatusChangedWebhookConsumer: ExternalCaseKey is null for AppraisalId {AppraisalId}, skipping", msg.AppraisalId);
            return;
        }

        await webhookService.SendAsync(
            eventId: msg.EventId,
            systemCode: "LendingStudio",
            eventType: "APPRAISAL_STATUS_CHANGED",
            externalCaseKey: keys.ExternalCaseKey,
            occurredAt: msg.OccurredAt,
            data: new { appraisalNumber = keys.AppraisalNumber, status },
            cancellationToken: context.CancellationToken);
    }
}
