using Integration.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Integration.Application.EventHandlers.Outbound;

public class QuotationReadyWebhookConsumer(
    IWebhookService webhookService,
    IAppraisalLookupService appraisalLookup,
    ILogger<QuotationReadyWebhookConsumer> logger)
    : IConsumer<ShortlistSentToRmIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ShortlistSentToRmIntegrationEvent> context)
    {
        var msg = context.Message;

        if (msg.AppraisalIds.Length == 0)
        {
            logger.LogWarning("QuotationReadyWebhookConsumer: no AppraisalIds on QuotationRequestId {QuotationRequestId}, skipping", msg.QuotationRequestId);
            return;
        }

        // Fan out one webhook per appraisal in the quotation.
        // Each fan-out needs a stable, unique eventId derived from (message.EventId, appraisalId)
        // so MassTransit retries produce the same eventId and the external system can dedup.
        foreach (var appraisalId in msg.AppraisalIds)
        {
            var keys = await appraisalLookup.GetKeysAsync(appraisalId, context.CancellationToken);
            if (keys is null)
            {
                logger.LogWarning("QuotationReadyWebhookConsumer: keys not found for AppraisalId {AppraisalId}, skipping", appraisalId);
                continue;
            }

            if (string.IsNullOrEmpty(keys.AppraisalNumber))
            {
                logger.LogWarning("QuotationReadyWebhookConsumer: AppraisalNumber is null for AppraisalId {AppraisalId}, skipping", appraisalId);
                continue;
            }

            if (string.IsNullOrEmpty(keys.ExternalCaseKey))
            {
                logger.LogWarning("QuotationReadyWebhookConsumer: ExternalCaseKey is null for AppraisalId {AppraisalId}, skipping", appraisalId);
                continue;
            }

            var fanOutEventId = DeterministicGuid.Create(msg.EventId, appraisalId);

            await webhookService.SendAsync(
                eventId: fanOutEventId,
                systemCode: "LendingStudio",
                eventType: "QUOTATION_READY",
                externalCaseKey: keys.ExternalCaseKey,
                occurredAt: msg.OccurredOn,
                data: new { appraisalNumber = keys.AppraisalNumber },
                cancellationToken: context.CancellationToken);
        }
    }
}
