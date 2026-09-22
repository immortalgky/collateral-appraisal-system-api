using Appraisal.Application.Services;
using Hangfire;
using MassTransit;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// On completion, kicks off generation of the post-approval Appraisal Summary so LOS pulls a document
/// that actually carries the committee decision. By the time this event is delivered the approval row
/// (appraisal.AppraisalReviews) and Status='Completed' are already committed — they share the single
/// SaveChanges in <see cref="AppraisalApprovedIntegrationEventHandler"/> — so the render sees complete data.
///
/// This consumer deliberately does nothing but enqueue. Rendering here would put a multi-second Puppeteer
/// job under the bus-level retry policy (Program.cs: Exponential(5, 1s, 30s, 5s)); five failures would
/// dead-letter the message and nothing would ever publish
/// <see cref="AppraisalResultReadyIntegrationEvent"/> — which is what the APPRAISAL_COMPLETED webhook now
/// waits on, so the case would stall silently on the LOS side. The Hangfire job owns the render and
/// guarantees it publishes that event in both the success and the failure path.
///
/// Auto-registered via the appraisalAssembly scan in Program.cs.
/// </summary>
public class AppraisalSummaryAutoAttachConsumer(
    IBackgroundJobClient backgroundJobClient,
    ILogger<AppraisalSummaryAutoAttachConsumer> logger,
    IDateTimeProvider dateTimeProvider,
    InboxGuard<AppraisalDbContext> inboxGuard)
    : IConsumer<AppraisalCompletedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalCompletedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        // Taken right after our claim landed, so ReleaseClaimAsync below can tell our row apart from one
        // another instance may legitimately reclaim if we stall past the stale window.
        var claimedBefore = dateTimeProvider.ApplicationNow;

        var msg = context.Message;

        string jobId;
        try
        {
            // CancellationToken.None is intentional: the background job outlives this consume scope.
            jobId = backgroundJobClient.Enqueue<AppraisalSummaryAutoAttachJob>(
                j => j.RunAsync(msg.AppraisalId, msg.RequestId, msg.CompletedAt, false, CancellationToken.None));
        }
        catch (Exception ex)
        {
            // The claim is already written, and every bus retry lands inside InboxGuard's 5-minute stale
            // window — so without releasing it each retry would "skip", ack the message, and leave the
            // appraisal with no summary and no APPRAISAL_COMPLETED webhook, silently. Release, then rethrow
            // so the retries (and finally the dead-letter queue) do their job.
            //
            // The cost, accepted knowingly: if the Enqueue write actually landed and only the acknowledgement
            // timed out, the retry enqueues a second job. Both run with force:false, and the "already
            // attached" check is an unlocked read, so if they overlap both render and both publish — two
            // webhooks and a duplicate document row. That is the lesser evil here: a duplicate reads to LOS
            // as "come and collect again" (DocumentReady never reaches them) and GetAppraisalResult serves
            // the newest row per type, whereas the silent stall has no webhook and nothing to find it by.
            // Note this is the opposite call from MarkAsProcessedAsync below, where the job is already safely
            // enqueued and a stuck claim really is cheaper.
            logger.LogError(ex,
                "AppraisalSummaryAutoAttachConsumer: failed to enqueue summary generation for AppraisalId={AppraisalId}",
                msg.AppraisalId);

            // CancellationToken.None, and swallowed: a cancelled or failing DELETE here would replace the
            // enqueue failure with a less useful exception and still leave the claim stuck — the caller needs
            // the original error either way.
            try
            {
                await inboxGuard.ReleaseClaimAsync(
                    context.MessageId, GetType().Name, claimedBefore, CancellationToken.None);
            }
            catch (Exception releaseEx)
            {
                logger.LogError(releaseEx,
                    "AppraisalSummaryAutoAttachConsumer: could not release the inbox claim for AppraisalId={AppraisalId}; "
                    + "bus retries will skip this message until the claim goes stale",
                    msg.AppraisalId);
            }

            throw;
        }

        logger.LogInformation(
            "AppraisalSummaryAutoAttachConsumer: enqueued summary generation job {JobId} for AppraisalId={AppraisalId}",
            jobId, msg.AppraisalId);

        // Swallowed on purpose. The job is already enqueued, so the work will happen regardless; letting a
        // transient SQL error here escape would fail the consumer for nothing. Worse, it would leave the claim
        // 'Processing' AND leave the message unacked — and a redelivery arriving after the 5-minute stale
        // window would reclaim it and enqueue a second render, i.e. a duplicate webhook to LOS. A stuck
        // 'Processing' row is the cheaper of the two, and the log line says where it came from.
        try
        {
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "AppraisalSummaryAutoAttachConsumer: summary job {JobId} is enqueued for AppraisalId={AppraisalId} "
                + "but the inbox claim could not be marked processed; it will sit in 'Processing'",
                jobId, msg.AppraisalId);
        }
    }
}
