using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Stamps CompletedAt + ApprovedByCommittee on the Appraisal aggregate, and upserts the single
/// committee-approval <see cref="AppraisalReview"/> outcome row for this appraisal (committee,
/// vote tally, decision meeting). Approval tier is derived from the committee in the read views.
/// Published by ApprovalActivity.ResumeActivityAsync when the final decision
/// (after any decisionConditions remap) resolves to "approve".
/// </summary>
public class AppraisalApprovedIntegrationEventHandler(
    ILogger<AppraisalApprovedIntegrationEventHandler> logger,
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork,
    AppraisalDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    InboxGuard<AppraisalDbContext> inboxGuard)
    : IConsumer<AppraisalApprovedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalApprovedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        // Identifies our own claim when releasing it below — see ReleaseClaimAsync.
        var claimedBefore = dateTimeProvider.ApplicationNow;

        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for AppraisalId: {AppraisalId} CommitteeCode: {CommitteeCode}",
            nameof(AppraisalApprovedIntegrationEvent),
            message.AppraisalId,
            message.CommitteeCode);

        try
        {
            // Load with assignments so MarkApprovedByCommittee can complete the active assignment.
            var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(message.AppraisalId, ct);

            if (appraisal is null)
            {
                // An appraisal that cannot be found while handling its own committee-approval event is a
                // data anomaly, not a normal outcome — the approval can never be stamped, so CompletedAt,
                // the AppraisalReviews row, AppraisalCompletedEvent and everything downstream of it
                // (including the summary and the LOS webhook) never happen for this appraisal.
                //
                // Deliberately NOT marked Processed: that would ack the message and make the skip permanent,
                // so a replay after the data is repaired could never re-stamp it. Throwing instead sends it
                // through the bus retries and finally to the dead-letter queue, where ops can see it and
                // replay it later. The claim is released on the way out by the catch below, so those retries
                // actually re-execute rather than being skipped.
                throw new NotFoundException(
                    $"Appraisal ({message.AppraisalId}) not found while stamping committee approval. "
                    + "AppraisalDbContext filters soft-deleted appraisals globally, so the row may exist "
                    + "with IsDeleted = 1 rather than be missing.");
            }

            appraisal.MarkApprovedByCommittee(message.CommitteeCode, message.ApprovedAt);

            await UpsertCommitteeReviewAsync(message, ct);

            await unitOfWork.SaveChangesAsync(ct);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);

            logger.LogInformation(
                "Successfully stamped committee approval for AppraisalId {AppraisalId} CommitteeCode {CommitteeCode}",
                message.AppraisalId,
                message.CommitteeCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing {IntegrationEvent} for AppraisalId: {AppraisalId}",
                nameof(AppraisalApprovedIntegrationEvent),
                message.AppraisalId);

            // The claim was committed before any of this ran. Rethrowing without releasing it makes the bus
            // retries useless: they all land inside InboxGuard's 5-minute stale window, each one is told to
            // skip, and the first of them acks the message. The committee approval would then never be
            // stamped — no CompletedAt, no AppraisalReviews row, no AppraisalCompletedEvent, so nothing
            // downstream runs — with a single log line and nothing in the dead-letter queue to find it by.
            // Realistic trigger is transient: a timeout on the GetByIdWithAllDataAsync load, or a deadlock
            // on SaveChanges — exactly what the retry policy exists to absorb.
            //
            // CancellationToken.None, and swallowed, so a failing release cannot replace the original error.
            try
            {
                // Drop the half-applied aggregate state before handing the message back. The failed attempt
                // leaves Appraisal Modified (CompletedAt already set in memory) and a new AppraisalReview
                // Added; ReleaseClaimAsync detaches only the InboxMessage. Bus-level UseMessageRetry sits
                // outside the consumer factory, so a retry should get a fresh scope and this should be moot —
                // but "should" is not worth betting committee-approval data on: if the scope were reused,
                // TryClaimAsync's own SaveChanges would commit that leftover state as a side effect of
                // inserting the claim row.
                dbContext.ChangeTracker.Clear();

                await inboxGuard.ReleaseClaimAsync(
                    context.MessageId, GetType().Name, claimedBefore, CancellationToken.None);
            }
            catch (Exception releaseEx)
            {
                logger.LogError(releaseEx,
                    "Could not release the inbox claim for AppraisalId {AppraisalId}; bus retries will skip "
                    + "this message until the claim goes stale",
                    message.AppraisalId);
            }

            throw;
        }
    }

    /// <summary>
    /// Upserts the single committee-approval review row for this appraisal (keyed on AppraisalId).
    /// Re-approval after a route-back updates the same row rather than inserting a duplicate.
    /// </summary>
    private async Task UpsertCommitteeReviewAsync(AppraisalApprovedIntegrationEvent message, CancellationToken ct)
    {
        var review = await dbContext.AppraisalReviews
            .FirstOrDefaultAsync(r => r.AppraisalId == message.AppraisalId, ct);

        if (review is null)
        {
            review = AppraisalReview.Create(message.AppraisalId);
            dbContext.AppraisalReviews.Add(review);
        }

        var committeeId = message.CommitteeId == Guid.Empty ? (Guid?)null : message.CommitteeId;

        review.RecordCommitteeApproval(
            committeeId: committeeId,
            approve: message.VotesApprove,
            reject: message.VotesReject,
            // No abstain concept in this domain; route-back votes occupy the spare tally column.
            abstain: message.VotesRouteBack,
            approvedAt: message.ApprovedAt,
            decisionMeetingId: message.DecisionMeetingId);
    }
}
