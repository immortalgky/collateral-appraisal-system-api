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
        // there is no ordering guarantee between the two integration events. Checking before the claim keeps
        // this expected miss off the inbox entirely, so its retries never depend on a claim being released.
        var reviewExists = await dbContext.AppraisalReviews
            .AnyAsync(r => r.AppraisalId == message.AppraisalId, ct);

        if (!reviewExists)
        {
            logger.LogWarning(
                "No AppraisalReview found for AppraisalId {AppraisalId} when handling {IntegrationEvent}; will retry",
                message.AppraisalId,
                nameof(AppraisalAcknowledgedIntegrationEvent));
            throw new InvalidOperationException(
                $"Review row not yet present for appraisal {message.AppraisalId}");
        }

        // Row exists — now claim for idempotency. A concurrent delivery loses the claim race and skips.
        await inboxGuard.RunOnceAsync(context.MessageId, GetType().Name, async _ =>
        {
            var review = await dbContext.AppraisalReviews.FirstAsync(r => r.AppraisalId == message.AppraisalId, ct);
            review.SetAcknowledgementMeeting(message.MeetingId);

            await unitOfWork.SaveChangesAsync(ct);

            logger.LogInformation(
                "Linked acknowledgement meeting {MeetingId} to Committee review for AppraisalId {AppraisalId}",
                message.MeetingId, message.AppraisalId);
        }, ct);
    }
}
