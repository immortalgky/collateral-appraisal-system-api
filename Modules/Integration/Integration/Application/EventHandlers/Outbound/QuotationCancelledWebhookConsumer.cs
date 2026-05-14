using Integration.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Integration.Application.EventHandlers.Outbound;

public class QuotationCancelledWebhookConsumer(
    IWebhookService webhookService,
    IAppraisalLookupService appraisalLookup,
    ILogger<QuotationCancelledWebhookConsumer> logger)
    : IConsumer<QuotationCancelledIntegrationEvent>
{
    public async Task Consume(ConsumeContext<QuotationCancelledIntegrationEvent> context)
    {
        var msg = context.Message;

        if (msg.AppraisalIds.Length == 0)
        {
            logger.LogWarning("QuotationCancelledWebhookConsumer: no AppraisalIds on QuotationRequestId {QuotationRequestId}, skipping", msg.QuotationRequestId);
            return;
        }

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
            logger.LogWarning("QuotationCancelledWebhookConsumer: no appraisal with ExternalSystem found on QuotationRequestId {QuotationRequestId}, skipping", msg.QuotationRequestId);
            return;
        }

        await webhookService.SendAsync(
            eventId: msg.EventId,
            systemCode: keys.ExternalSystem!,
            eventType: "QUOTATION_CANCELLED",
            externalCaseKey: keys.ExternalCaseKey!,
            occurredAt: msg.OccurredOn,
            data: new
            {
                quotationId = msg.QuotationRequestId,
                reason = msg.Reason,
                rmUsername = msg.RmUsername
            },
            cancellationToken: context.CancellationToken);
    }
}
