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
      IConsumer<AppraisalStatusChangedIntegrationEvent>,
      IConsumer<AppraisalPmaUpdatedIntegrationEvent>
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
    // APPRAISAL_PMA_UPDATED — pushes the committed PMA data (one payload per land title, or one
    // for the condo record) to the external system named by the owning Request's ExternalSystem
    // (e.g. "LOS"), routed dynamically (NOT a hardcoded SystemCode — "LOS" already has an
    // unrelated catch-all HMAC subscription serving the other 9 outbound events; the PMA push
    // resolves its own dedicated (SystemCode, EventType) subscription via WebhookService), then
    // round-trips the aggregate per-property outcome back to the Appraisal module.
    // -------------------------------------------------------------------------
    public async Task Consume(ConsumeContext<AppraisalPmaUpdatedIntegrationEvent> context)
    {
        var msg = context.Message;

        // data is null when the header query's join to request.Requests finds nothing — either the
        // property was deleted, OR (also observed) a reappraisal/AS400-originated appraisal has no
        // linked Request row at all. Either way there is nothing left to sync, but the property MAY
        // still be sitting at "Pending" from the save — publish NotSynced so it resolves instead of
        // sticking forever (every guard below in this method must publish for the same reason).
        var data = await appraisalLookup.GetPmaUpdateDataAsync(msg.AppraisalId, msg.PropertyId, context.CancellationToken);
        if (data is null)
        {
            logger.LogWarning(
                "WebhookDispatchConsumer [PMA_UPDATED]: property/request not found for AppraisalId {AppraisalId} PropertyId {PropertyId} — publishing NotSynced",
                msg.AppraisalId, msg.PropertyId);

            await context.Publish(new PmaExternalSyncStatusChangedIntegrationEvent
            {
                AppraisalId = msg.AppraisalId,
                PropertyId = msg.PropertyId,
                Status = PmaExternalSyncStatus.NotSynced,
                Error = null
            }, context.CancellationToken);
            return;
        }

        if (string.IsNullOrEmpty(data.ExternalSystem))
        {
            // Internal appraisal (no external LOS/host system on the request) — nothing to sync.
            // Clear the Pending stamp set on save; NotSynced is a resolved terminal state, not a
            // failure.
            logger.LogInformation(
                "WebhookDispatchConsumer [PMA_UPDATED]: no ExternalSystem on the request for AppraisalId {AppraisalId} PropertyId {PropertyId} — nothing to sync",
                msg.AppraisalId, msg.PropertyId);

            await context.Publish(new PmaExternalSyncStatusChangedIntegrationEvent
            {
                AppraisalId = msg.AppraisalId,
                PropertyId = msg.PropertyId,
                Status = PmaExternalSyncStatus.NotSynced,
                Error = null
            }, context.CancellationToken);
            return;
        }

        if (string.IsNullOrEmpty(data.AppraisalNumber))
        {
            const string error = "AppraisalNumber is missing — cannot build casReportNo for the LOS payload.";
            logger.LogWarning(
                "WebhookDispatchConsumer [PMA_UPDATED]: {Error} AppraisalId {AppraisalId} PropertyId {PropertyId}",
                error, msg.AppraisalId, msg.PropertyId);

            await context.Publish(new PmaExternalSyncStatusChangedIntegrationEvent
            {
                AppraisalId = msg.AppraisalId,
                PropertyId = msg.PropertyId,
                Status = PmaExternalSyncStatus.Failed,
                Error = error
            }, context.CancellationToken);
            return;
        }

        var payloads = LosPmaPayloadMapper.Map(data);
        if (payloads.Count == 0)
        {
            // Not a failure — e.g. prices were saved before any title/condo detail was entered.
            // Nothing to send yet; resolve Pending to NotSynced rather than Failed so it doesn't
            // read as a delivery error the user needs to retry.
            logger.LogInformation(
                "WebhookDispatchConsumer [PMA_UPDATED]: no land titles or condo detail to map yet for AppraisalId {AppraisalId} PropertyId {PropertyId} — nothing to sync",
                msg.AppraisalId, msg.PropertyId);

            await context.Publish(new PmaExternalSyncStatusChangedIntegrationEvent
            {
                AppraisalId = msg.AppraisalId,
                PropertyId = msg.PropertyId,
                Status = PmaExternalSyncStatus.NotSynced,
                Error = null
            }, context.CancellationToken);
            return;
        }

        var allOk = true;
        string? firstError = null;

        foreach (var payload in payloads)
        {
            try
            {
                // LOS receives its own fields at the top level (no CAS envelope) — wrapInEnvelope: false.
                var outcome = await webhookService.SendAsync(
                    eventId: Guid.CreateVersion7(),
                    systemCode: data.ExternalSystem!, // verified non-empty above
                    eventType: "APPRAISAL_PMA_UPDATED",
                    externalCaseKey: data.AppraisalNumber,
                    occurredAt: msg.OccurredOn,
                    data: payload,
                    cancellationToken: context.CancellationToken,
                    wrapInEnvelope: false);

                if (!outcome.Success)
                {
                    allOk = false;
                    firstError ??= outcome.Error;
                }
            }
            catch (Exception ex)
            {
                // Must not rethrow: an exception here would abort the loop AND make MassTransit
                // retry-redeliver the whole message, re-POSTing titles that already succeeded above.
                // Record the failure and keep going so every title gets exactly one attempt.
                logger.LogError(ex,
                    "WebhookDispatchConsumer [PMA_UPDATED]: unexpected exception sending a title for AppraisalId {AppraisalId} PropertyId {PropertyId}",
                    msg.AppraisalId, msg.PropertyId);
                allOk = false;
                firstError ??= ex.Message;
            }
        }

        await context.Publish(new PmaExternalSyncStatusChangedIntegrationEvent
        {
            AppraisalId = msg.AppraisalId,
            PropertyId = msg.PropertyId,
            Status = allOk ? PmaExternalSyncStatus.Delivered : PmaExternalSyncStatus.Failed,
            Error = firstError
        }, context.CancellationToken);
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
