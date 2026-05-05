using Integration.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Integration.Application.EventHandlers.Outbound;

public class AppraisalCancelledWebhookConsumer(
    IWebhookService webhookService,
    IAppraisalLookupService appraisalLookup,
    ILogger<AppraisalCancelledWebhookConsumer> logger)
    : IConsumer<AppraisalCancelIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalCancelIntegrationEvent> context)
    {
        var msg = context.Message;

        if (msg.CorrelationId == Guid.Empty)
        {
            logger.LogWarning("AppraisalCancelledWebhookConsumer: CorrelationId is empty, skipping");
            return;
        }

        var keys = await appraisalLookup.GetKeysByRequestIdAsync(msg.CorrelationId, context.CancellationToken);
        if (keys is null)
        {
            logger.LogWarning("AppraisalCancelledWebhookConsumer: keys not found for RequestId {RequestId}, skipping", msg.CorrelationId);
            return;
        }

        if (string.IsNullOrEmpty(keys.AppraisalNumber))
        {
            logger.LogWarning("AppraisalCancelledWebhookConsumer: AppraisalNumber is null for RequestId {RequestId}, skipping", msg.CorrelationId);
            return;
        }

        if (string.IsNullOrEmpty(keys.ExternalCaseKey))
        {
            logger.LogWarning("AppraisalCancelledWebhookConsumer: ExternalCaseKey is null for RequestId {RequestId}, skipping", msg.CorrelationId);
            return;
        }

        if (string.IsNullOrEmpty(keys.ExternalSystem))
        {
            logger.LogWarning("AppraisalCancelledWebhookConsumer: ExternalSystem is null for RequestId {RequestId}, skipping", msg.CorrelationId);
            return;
        }

        await webhookService.SendAsync(
            eventId: msg.EventId,
            systemCode: keys.ExternalSystem,
            eventType: "APPRAISAL_CANCELLED",
            externalCaseKey: keys.ExternalCaseKey,
            occurredAt: msg.CancelledAt,
            data: new { appraisalNumber = keys.AppraisalNumber },
            cancellationToken: context.CancellationToken);
    }
}
