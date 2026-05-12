using Integration.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Integration.Application.EventHandlers.Outbound;

/// <summary>
/// Single MassTransit consumer bound to the "webhook-dispatch" endpoint.
/// Handles both <see cref="AppraisalCreatedIntegrationEvent"/> and
/// <see cref="AppraisalStatusChangedIntegrationEvent"/> on the same partitioned endpoint
/// so that per-appraisal ordering is guaranteed.
///
/// <see cref="ExcludeFromConfigureEndpointsAttribute"/> prevents <c>ConfigureEndpoints</c>
/// from auto-creating per-message-type queues — the endpoint is wired manually in Program.cs.
/// </summary>
[ExcludeFromConfigureEndpoints]
public class WebhookDispatchConsumer(
    IWebhookService webhookService,
    IAppraisalLookupService appraisalLookup,
    ILogger<WebhookDispatchConsumer> logger)
    : IConsumer<AppraisalCreatedIntegrationEvent>,
      IConsumer<AppraisalStatusChangedIntegrationEvent>
{
    // -------------------------------------------------------------------------
    // APPRAISAL_CREATED
    // -------------------------------------------------------------------------
    public async Task Consume(ConsumeContext<AppraisalCreatedIntegrationEvent> context)
    {
        var msg = context.Message;

        var keys = await appraisalLookup.GetKeysAsync(msg.AppraisalId, context.CancellationToken);
        if (keys is null)
        {
            logger.LogWarning("WebhookDispatchConsumer [CREATED]: keys not found for AppraisalId {AppraisalId}, skipping", msg.AppraisalId);
            return;
        }

        if (!HasRequiredKeys(keys, msg.AppraisalId, "CREATED"))
            return;

        await webhookService.SendAsync(
            eventId: msg.EventId,
            systemCode: keys.ExternalSystem!,
            eventType: "APPRAISAL_CREATED",
            externalCaseKey: keys.ExternalCaseKey!,
            occurredAt: msg.OccurredOn,
            data: new { appraisalNumber = keys.AppraisalNumber, requestId = msg.RequestId },
            cancellationToken: context.CancellationToken);
    }

    // -------------------------------------------------------------------------
    // APPRAISAL_STATUS_CHANGED
    // -------------------------------------------------------------------------
    public async Task Consume(ConsumeContext<AppraisalStatusChangedIntegrationEvent> context)
    {
        var msg = context.Message;

        var newExternal = IntegrationStatusMap.Map(msg.Status);
        if (newExternal is null)
        {
            logger.LogDebug("WebhookDispatchConsumer [STATUS_CHANGED]: unmapped internal status '{Status}' for AppraisalId {AppraisalId}, skipping",
                msg.Status, msg.AppraisalId);
            return;
        }

        // Resolve previous external bucket once (used both for suppression check and payload).
        var prevExternal = msg.PreviousStatus is not null ? IntegrationStatusMap.Map(msg.PreviousStatus) : null;

        // Suppress intra-bucket transitions (e.g. InProgress → UnderReview both map to IN_PROGRESS)
        if (msg.PreviousStatus is not null && prevExternal == newExternal)
        {
            logger.LogDebug("WebhookDispatchConsumer [STATUS_CHANGED]: intra-bucket transition '{Prev}' → '{New}' (both {Bucket}) for AppraisalId {AppraisalId}, suppressed",
                msg.PreviousStatus, msg.Status, newExternal, msg.AppraisalId);
            return;
        }

        var keys = await appraisalLookup.GetKeysAsync(msg.AppraisalId, context.CancellationToken);
        if (keys is null)
        {
            logger.LogWarning("WebhookDispatchConsumer [STATUS_CHANGED]: keys not found for AppraisalId {AppraisalId}, skipping", msg.AppraisalId);
            return;
        }

        if (!HasRequiredKeys(keys, msg.AppraisalId, "STATUS_CHANGED"))
            return;

        await webhookService.SendAsync(
            eventId: msg.EventId,
            systemCode: keys.ExternalSystem!,
            eventType: "APPRAISAL_STATUS_CHANGED",
            externalCaseKey: keys.ExternalCaseKey!,
            occurredAt: msg.OccurredOn,
            data: new
            {
                appraisalNumber = keys.AppraisalNumber,
                previousStatus = prevExternal,
                status = newExternal
            },
            cancellationToken: context.CancellationToken);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private bool HasRequiredKeys(AppraisalKeys keys, Guid appraisalId, string eventLabel)
    {
        if (string.IsNullOrEmpty(keys.AppraisalNumber))
        {
            logger.LogWarning("WebhookDispatchConsumer [{Label}]: AppraisalNumber is null for AppraisalId {AppraisalId}, skipping", eventLabel, appraisalId);
            return false;
        }

        if (string.IsNullOrEmpty(keys.ExternalCaseKey))
        {
            logger.LogWarning("WebhookDispatchConsumer [{Label}]: ExternalCaseKey is null for AppraisalId {AppraisalId}, skipping", eventLabel, appraisalId);
            return false;
        }

        if (string.IsNullOrEmpty(keys.ExternalSystem))
        {
            logger.LogWarning("WebhookDispatchConsumer [{Label}]: ExternalSystem is null for AppraisalId {AppraisalId}, skipping", eventLabel, appraisalId);
            return false;
        }

        return true;
    }
}
