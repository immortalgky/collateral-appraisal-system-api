using Integration.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Integration.Application.EventHandlers.Outbound;

public class AppraisalStatusChangedWebhookConsumer(
    IWebhookService webhookService,
    IAppraisalLookupService appraisalLookup,
    ILogger<AppraisalStatusChangedWebhookConsumer> logger)
    : IConsumer<WorkflowTransitionedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<WorkflowTransitionedIntegrationEvent> context)
    {
        var msg = context.Message;

        if (msg.AppraisalId is null)
            return;

        var status = IntegrationStatusMap.Map(msg.DestinationActivityId);
        if (status is null)
            return;

        var keys = await appraisalLookup.GetKeysAsync(msg.AppraisalId.Value, context.CancellationToken);
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

        if (string.IsNullOrEmpty(keys.ExternalSystem))
        {
            logger.LogWarning("AppraisalStatusChangedWebhookConsumer: ExternalSystem is null for AppraisalId {AppraisalId}, skipping", msg.AppraisalId);
            return;
        }

        await webhookService.SendAsync(
            eventId: msg.EventId,
            systemCode: keys.ExternalSystem,
            eventType: "APPRAISAL_STATUS_CHANGED",
            externalCaseKey: keys.ExternalCaseKey,
            occurredAt: msg.CompletedAt,
            data: new { appraisalNumber = keys.AppraisalNumber, status },
            cancellationToken: context.CancellationToken);
    }
}
