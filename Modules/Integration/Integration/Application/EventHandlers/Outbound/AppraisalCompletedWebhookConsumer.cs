using Integration.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Integration.Application.EventHandlers.Outbound;

public class AppraisalCompletedWebhookConsumer(
    IWebhookService webhookService,
    IAppraisalLookupService appraisalLookup,
    ILogger<AppraisalCompletedWebhookConsumer> logger)
    : IConsumer<AppraisalCompletedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalCompletedIntegrationEvent> context)
    {
        var msg = context.Message;

        var keys = await appraisalLookup.GetKeysAsync(msg.AppraisalId, context.CancellationToken);
        if (keys is null)
        {
            logger.LogWarning("AppraisalCompletedWebhookConsumer: keys not found for AppraisalId {AppraisalId}, skipping", msg.AppraisalId);
            return;
        }

        if (string.IsNullOrEmpty(keys.AppraisalNumber))
        {
            logger.LogWarning("AppraisalCompletedWebhookConsumer: AppraisalNumber is null for AppraisalId {AppraisalId}, skipping", msg.AppraisalId);
            return;
        }

        if (string.IsNullOrEmpty(keys.ExternalCaseKey))
        {
            logger.LogWarning("AppraisalCompletedWebhookConsumer: ExternalCaseKey is null for AppraisalId {AppraisalId}, skipping", msg.AppraisalId);
            return;
        }

        await webhookService.SendAsync(
            eventId: msg.EventId,
            systemCode: "LendingStudio",
            eventType: "APPRAISAL_COMPLETED",
            externalCaseKey: keys.ExternalCaseKey,
            occurredAt: msg.CompletedAt,
            data: new { appraisalNumber = keys.AppraisalNumber },
            cancellationToken: context.CancellationToken);
    }
}
