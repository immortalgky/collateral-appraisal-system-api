using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

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
    InboxGuard<AppraisalDbContext> inboxGuard)
    : IConsumer<AppraisalApprovedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalApprovedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

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
                logger.LogWarning(
                    "Appraisal {AppraisalId} not found when handling {IntegrationEvent}",
                    message.AppraisalId,
                    nameof(AppraisalApprovedIntegrationEvent));
                return;
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
