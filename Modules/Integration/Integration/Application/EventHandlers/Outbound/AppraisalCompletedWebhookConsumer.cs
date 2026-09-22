using Integration.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Integration.Application.EventHandlers.Outbound;

/// <summary>
/// Pushes APPRAISAL_COMPLETED to the originating external system (LOS).
///
/// Listens to <see cref="AppraisalResultReadyIntegrationEvent"/> rather than
/// AppraisalCompletedIntegrationEvent on purpose. The completion event fans out to several consumers in
/// parallel, one of which renders the Appraisal Summary PDF — firing the webhook off the completion
/// event meant LOS was told to come and collect while that render was still running, so
/// GET /api/v2/appraisals/{no}/result handed back the pre-approval document (or none). Waiting for the
/// result-ready event costs roughly half a minute and closes that window.
///
/// The event is published in both the success and the permanent-failure path, so a broken render delays
/// this webhook but never cancels it. DocumentReady = false means LOS will collect an incomplete
/// package — logged as a warning; an admin re-running the generation publishes the event again and LOS
/// is re-notified.
///
/// The other completion consumers (CollateralMaster upsert, dashboard counters, request.Complete) still
/// listen to AppraisalCompletedIntegrationEvent — none of them should wait on a PDF.
/// </summary>
public class AppraisalCompletedWebhookConsumer(
    IWebhookService webhookService,
    IAppraisalLookupService appraisalLookup,
    ILogger<AppraisalCompletedWebhookConsumer> logger)
    : IConsumer<AppraisalResultReadyIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalResultReadyIntegrationEvent> context)
    {
        var msg = context.Message;

        if (!msg.DocumentReady)
        {
            logger.LogWarning(
                "AppraisalCompletedWebhookConsumer: sending APPRAISAL_COMPLETED for AppraisalId {AppraisalId} " +
                "WITHOUT a post-approval summary document (reason: {FailureReason}). " +
                "The consumer will fetch an incomplete result package until the summary is regenerated.",
                msg.AppraisalId, msg.FailureReason);
        }

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

        if (string.IsNullOrEmpty(keys.ExternalSystem))
        {
            logger.LogWarning("AppraisalCompletedWebhookConsumer: ExternalSystem is null for AppraisalId {AppraisalId}, skipping", msg.AppraisalId);
            return;
        }

        // OccurredOn, not CompletedAt: the envelope's occurredAt describes when this notification happened,
        // and IntegrationEventOutbox stamps it with ApplicationNow at the publish boundary. On the automatic
        // path the two are seconds apart, but an admin regenerate months after closing would otherwise send
        // an event dated back to the committee decision — which a consumer that filters on occurredAt could
        // discard as stale, silently killing the one path that exists to re-notify them.
        await webhookService.SendAsync(
            eventId: msg.EventId,
            systemCode: keys.ExternalSystem,
            eventType: "APPRAISAL_COMPLETED",
            externalCaseKey: keys.ExternalCaseKey,
            occurredAt: msg.OccurredOn,
            data: new { appraisalNumber = keys.AppraisalNumber },
            cancellationToken: context.CancellationToken);
    }
}
