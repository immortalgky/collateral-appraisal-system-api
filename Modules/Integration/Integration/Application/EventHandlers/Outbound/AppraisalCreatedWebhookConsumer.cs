using Integration.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Integration.Application.EventHandlers.Outbound;

public class AppraisalCreatedWebhookConsumer(
    IWebhookService webhookService,
    IAppraisalLookupService appraisalLookup,
    ILogger<AppraisalCreatedWebhookConsumer> logger)
    : IConsumer<AppraisalCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalCreatedIntegrationEvent> context)
    {
        var msg = context.Message;

        var keys = await appraisalLookup.GetKeysAsync(msg.AppraisalId, context.CancellationToken);
        if (keys is null)
        {
            logger.LogWarning("AppraisalCreatedWebhookConsumer: keys not found for AppraisalId {AppraisalId}, skipping webhook", msg.AppraisalId);
            return;
        }

        if (string.IsNullOrEmpty(keys.AppraisalNumber))
        {
            logger.LogWarning("AppraisalCreatedWebhookConsumer: AppraisalNumber is null for AppraisalId {AppraisalId}, skipping webhook", msg.AppraisalId);
            return;
        }

        if (string.IsNullOrEmpty(keys.ExternalCaseKey))
        {
            logger.LogWarning("AppraisalCreatedWebhookConsumer: ExternalCaseKey is null for AppraisalId {AppraisalId}, skipping webhook", msg.AppraisalId);
            return;
        }

        await webhookService.SendAsync(
            eventId: msg.EventId,
            systemCode: "LendingStudio",
            eventType: "APPRAISAL_CREATED",
            externalCaseKey: keys.ExternalCaseKey,
            occurredAt: msg.OccurredOn,
            data: new { appraisalNumber = keys.AppraisalNumber, requestId = msg.RequestId },
            cancellationToken: context.CancellationToken);
    }
}
