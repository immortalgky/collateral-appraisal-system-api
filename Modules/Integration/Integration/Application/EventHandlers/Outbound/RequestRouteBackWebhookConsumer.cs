using Integration.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Integration.Application.EventHandlers.Outbound;

public class RequestRouteBackWebhookConsumer(
    IWebhookService webhookService,
    IAppraisalLookupService appraisalLookup,
    ILogger<RequestRouteBackWebhookConsumer> logger)
    : IConsumer<TaskAssignedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<TaskAssignedIntegrationEvent> context)
    {
        var msg = context.Message;

        // Only fire for route-back transitions arriving at appraisal-initiation.
        if (!string.Equals(msg.ActivityId, "appraisal-initiation", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(msg.Movement, "B", StringComparison.OrdinalIgnoreCase)
            || msg.AppraisalId is null)
        {
            return;
        }

        var keys = await appraisalLookup.GetKeysAsync(msg.AppraisalId.Value, context.CancellationToken);
        if (keys is null)
        {
            logger.LogWarning("RequestRouteBackWebhookConsumer: keys not found for AppraisalId {AppraisalId}, skipping", msg.AppraisalId);
            return;
        }

        if (string.IsNullOrEmpty(keys.AppraisalNumber))
        {
            logger.LogWarning("RequestRouteBackWebhookConsumer: AppraisalNumber is null for AppraisalId {AppraisalId}, skipping", msg.AppraisalId);
            return;
        }

        if (string.IsNullOrEmpty(keys.ExternalCaseKey))
        {
            logger.LogWarning("RequestRouteBackWebhookConsumer: ExternalCaseKey is null for AppraisalId {AppraisalId}, skipping", msg.AppraisalId);
            return;
        }

        var reasonCode = msg.ReasonCode;
        var reason = msg.Reason;

        if (string.IsNullOrEmpty(reasonCode))
        {
            logger.LogWarning("RequestRouteBackWebhookConsumer: ReasonCode is missing for AppraisalId {AppraisalId}, sending with empty reason", msg.AppraisalId);
        }

        await webhookService.SendAsync(
            eventId: context.Message.EventId,
            systemCode: "LendingStudio",
            eventType: "REQUEST_ROUTE_BACK",
            externalCaseKey: keys.ExternalCaseKey,
            occurredAt: msg.AssignedAt,
            data: new
            {
                appraisalNumber = keys.AppraisalNumber,
                reasonCode = reasonCode ?? string.Empty,
                reason = reason ?? string.Empty
            },
            cancellationToken: context.CancellationToken);
    }
}
