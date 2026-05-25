using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Links the appraisal's Committee <see cref="AppraisalReview"/> row to the meeting in which a
/// sub-committee approval was acknowledged. Published by the Workflow Meetings module when an
/// acknowledgement meeting ends.
/// </summary>
public class AppraisalAcknowledgedIntegrationEventHandler(
    ILogger<AppraisalAcknowledgedIntegrationEventHandler> logger,
    IAppraisalUnitOfWork unitOfWork,
    AppraisalDbContext dbContext,
    InboxGuard<AppraisalDbContext> inboxGuard)
    : IConsumer<AppraisalAcknowledgedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalAcknowledgedIntegrationEvent> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        // Look up the review row BEFORE claiming the inbox. The approval event creates this row and
        // there is no ordering guarantee between the two integration events. If we claimed first and
        // then threw, redelivery would see our own (non-stale) Processing inbox row and skip forever
        // (InboxGuard's 5-min stale window > MassTransit's retry budget) — silently losing the link.
        var review = await dbContext.AppraisalReviews
            .FirstOrDefaultAsync(r => r.AppraisalId == message.AppraisalId, ct);

        if (review is null)
        {
            logger.LogWarning(
                "No AppraisalReview found for AppraisalId {AppraisalId} when handling {IntegrationEvent}; will retry",
                message.AppraisalId,
                nameof(AppraisalAcknowledgedIntegrationEvent));
            throw new InvalidOperationException(
                $"Review row not yet present for appraisal {message.AppraisalId}");
        }

        // Row exists — now claim for idempotency. A concurrent delivery loses the claim race and skips.
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, ct))
            return;

        review.SetAcknowledgementMeeting(message.MeetingId);

        await unitOfWork.SaveChangesAsync(ct);
        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);

        logger.LogInformation(
            "Linked acknowledgement meeting {MeetingId} to Committee review for AppraisalId {AppraisalId}",
            message.MeetingId, message.AppraisalId);
    }
}
