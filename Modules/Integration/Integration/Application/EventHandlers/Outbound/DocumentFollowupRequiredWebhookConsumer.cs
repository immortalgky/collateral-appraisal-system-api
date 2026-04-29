using Integration.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Integration.Application.EventHandlers.Outbound;

public class DocumentFollowupRequiredWebhookConsumer(
    IWebhookService webhookService,
    IAppraisalLookupService appraisalLookup,
    ILogger<DocumentFollowupRequiredWebhookConsumer> logger)
    : IConsumer<DocumentFollowupRequiredIntegrationEvent>
{
    public async Task Consume(ConsumeContext<DocumentFollowupRequiredIntegrationEvent> context)
    {
        var msg = context.Message;

        var keys = await appraisalLookup.GetKeysAsync(msg.AppraisalId, context.CancellationToken);
        if (keys is null)
        {
            logger.LogWarning("DocumentFollowupRequiredWebhookConsumer: keys not found for AppraisalId {AppraisalId}, skipping", msg.AppraisalId);
            return;
        }

        if (string.IsNullOrEmpty(keys.AppraisalNumber))
        {
            logger.LogWarning("DocumentFollowupRequiredWebhookConsumer: AppraisalNumber is null for AppraisalId {AppraisalId}, skipping", msg.AppraisalId);
            return;
        }

        if (string.IsNullOrEmpty(keys.ExternalCaseKey))
        {
            logger.LogWarning("DocumentFollowupRequiredWebhookConsumer: ExternalCaseKey is null for AppraisalId {AppraisalId}, skipping", msg.AppraisalId);
            return;
        }

        await webhookService.SendAsync(
            eventId: msg.EventId,
            systemCode: "LendingStudio",
            eventType: "DOCUMENT_FOLLOWUP_REQUIRED",
            externalCaseKey: keys.ExternalCaseKey,
            occurredAt: msg.OccurredOn,
            data: new
            {
                appraisalNumber = keys.AppraisalNumber,
                reasonCode = msg.ReasonCode,
                reason = msg.Reason
            },
            cancellationToken: context.CancellationToken);
    }
}
