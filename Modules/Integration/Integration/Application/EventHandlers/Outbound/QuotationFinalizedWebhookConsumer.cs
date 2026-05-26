using System.Text.Json;
using Integration.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Integration.Application.EventHandlers.Outbound;

public class QuotationFinalizedWebhookConsumer(
    IWebhookService webhookService,
    IAppraisalLookupService appraisalLookup,
    IQuotationFinalizeLookupService finalizeLookup,
    ILogger<QuotationFinalizedWebhookConsumer> logger)
    : IConsumer<QuotationFinalizedIntegrationEvent>
{
    private static readonly JsonSerializerOptions ReasonJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task Consume(ConsumeContext<QuotationFinalizedIntegrationEvent> context)
    {
        var msg = context.Message;

        if (msg.AppraisalIds.Length == 0)
        {
            logger.LogWarning("QuotationFinalizedWebhookConsumer: no AppraisalIds on QuotationRequestId {QuotationRequestId}, skipping", msg.QuotationRequestId);
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
            logger.LogWarning("QuotationFinalizedWebhookConsumer: no appraisal with ExternalSystem found on QuotationRequestId {QuotationRequestId}, skipping", msg.QuotationRequestId);
            return;
        }

        var snapshot = await finalizeLookup.GetSnapshotAsync(
            msg.QuotationRequestId, msg.WinningQuotationId, context.CancellationToken);

        if (snapshot is null)
        {
            logger.LogWarning("QuotationFinalizedWebhookConsumer: no quotation snapshot for WinningQuotationId {WinningQuotationId} (QuotationRequestId {QuotationRequestId}), skipping", msg.WinningQuotationId, msg.QuotationRequestId);
            return;
        }

        // The external contract embeds the finalized-quotation detail as an escaped JSON
        // string inside data.reason, mirroring the QUOTATION_CANCELLED payload shape.
        var reason = JsonSerializer.Serialize(snapshot, ReasonJsonOptions);

        await webhookService.SendAsync(
            eventId: msg.EventId,
            systemCode: keys.ExternalSystem!,
            eventType: "QUOTATION_FINALIZED",
            externalCaseKey: keys.ExternalCaseKey!,
            occurredAt: msg.OccurredOn,
            data: new
            {
                quotationId = msg.QuotationRequestId,
                reason
            },
            cancellationToken: context.CancellationToken);
    }
}
