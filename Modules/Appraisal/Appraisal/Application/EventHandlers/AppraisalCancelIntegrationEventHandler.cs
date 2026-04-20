using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Cancels the Appraisal aggregate when a workflow activity completes with Movement='C'.
/// Published by TaskCompletedDomainEventHandler in the Workflow module.
/// </summary>
public class AppraisalCancelIntegrationEventHandler(
    ILogger<AppraisalCancelIntegrationEventHandler> logger,
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork,
    InboxGuard<AppraisalDbContext> inboxGuard)
    : IConsumer<AppraisalCancelIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalCancelIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for AppraisalId: {AppraisalId} CancelledBy: {CancelledBy}",
            nameof(AppraisalCancelIntegrationEvent),
            message.AppraisalId,
            message.CancelledBy);

        try
        {
            var appraisal = await appraisalRepository.GetByIdAsync(message.AppraisalId, ct);

            if (appraisal is null)
            {
                logger.LogWarning(
                    "Appraisal {AppraisalId} not found when handling {IntegrationEvent}",
                    message.AppraisalId,
                    nameof(AppraisalCancelIntegrationEvent));
                await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
                return;
            }

            // Idempotent short-circuit: already in a terminal state
            if (appraisal.Status == AppraisalStatus.Cancelled || appraisal.Status == AppraisalStatus.Completed)
            {
                logger.LogInformation(
                    "AppraisalId {AppraisalId} is already in status {Status}; skipping cancellation",
                    message.AppraisalId, appraisal.Status.Code);
                await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
                return;
            }

            appraisal.Cancel(message.CancelledBy, message.CancelledAt, message.CancelReason);

            await unitOfWork.SaveChangesAsync(ct);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);

            logger.LogInformation(
                "Successfully cancelled AppraisalId {AppraisalId} by {CancelledBy} at {CancelledAt}",
                message.AppraisalId,
                message.CancelledBy,
                message.CancelledAt);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing {IntegrationEvent} for AppraisalId: {AppraisalId}",
                nameof(AppraisalCancelIntegrationEvent),
                message.AppraisalId);

            throw;
        }
    }
}
