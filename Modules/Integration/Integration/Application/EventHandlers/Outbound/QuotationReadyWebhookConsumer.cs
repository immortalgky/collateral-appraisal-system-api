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

        // Find the first appraisal in the quotation that belongs to an external system.
        AppraisalKeys? keys = null;
        foreach (var appraisalId in msg.AppraisalIds)
        {
            var candidate = await appraisalLookup.GetKeysAsync(appraisalId, context.CancellationToken);
            if (candidate is not null
                && !string.IsNullOrEmpty(candidate.ExternalCaseKey)
                && !string.IsNullOrEmpty(candidate.ExternalSystem))
            {
                keys = candidate;
                break;
            }
        }

        if (keys is null)
        {
            logger.LogWarning("QuotationReadyWebhookConsumer: no appraisal with ExternalSystem found on QuotationRequestId {QuotationRequestId}, skipping", msg.QuotationRequestId);
            return;
        }

        await webhookService.SendAsync(
            eventId: msg.EventId,
            systemCode: keys.ExternalSystem!,
            eventType: "QUOTATION_READY",
            externalCaseKey: keys.ExternalCaseKey!,
            occurredAt: msg.OccurredOn,
            data: new { quotationId = msg.QuotationRequestId, rmUsername = msg.RmUsername },
            cancellationToken: context.CancellationToken);
    }
}
