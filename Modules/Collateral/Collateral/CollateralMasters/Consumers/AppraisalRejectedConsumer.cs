using Collateral.CollateralMasters.Models;
using Collateral.Data;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;

namespace Collateral.CollateralMasters.Consumers;

/// <summary>
/// Listens for <see cref="AppraisalRejectedIntegrationEvent"/> and spools a
/// <see cref="PendingCollateralResult"/> row so the next <c>CollateralResultExportJob</c>
/// run emits a status-R record to the AS400 Collateral Result interface.
///
/// Idempotency: InboxGuard deduplicates retried/concurrent delivery; the unique index on
/// <c>PendingCollateralResults.AppraisalId</c> is a second-line guard.
///
/// HostCollateralId is NULL at this point — no CollateralEngagement exists before approval,
/// so the AS400 collateral key cannot be resolved. The R row will be exported with a blank
/// CCDCID field. See PendingCollateralResult for the full TODO note.
/// </summary>
public class AppraisalRejectedConsumer(
    CollateralDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<AppraisalRejectedConsumer> logger,
    InboxGuard<CollateralDbContext> inboxGuard)
    : IConsumer<AppraisalRejectedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalRejectedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var msg = context.Message;
        var ct = context.CancellationToken;

        // Second-line idempotency beyond InboxGuard: a re-rejection of the same appraisal
        // (route-back → re-review → reject again) arrives with a DIFFERENT MessageId, so InboxGuard
        // does not dedupe it. Without this check the unique index on AppraisalId throws an unhandled
        // DbUpdateException. First rejection wins; subsequent ones are acked as no-ops.
        var alreadySpooled = await dbContext.PendingCollateralResults
            .AnyAsync(p => p.AppraisalId == msg.AppraisalId, ct);
        if (alreadySpooled)
        {
            logger.LogInformation(
                "AppraisalRejectedConsumer: PendingCollateralResult already spooled for AppraisalId={AppraisalId} — skipping",
                msg.AppraisalId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        logger.LogInformation(
            "AppraisalRejectedConsumer: spooling R result for AppraisalId={AppraisalId} AppraisalNo={AppraisalNo}",
            msg.AppraisalId, msg.AppraisalNo);

        var rejectedAt = msg.RejectedAt == default
            ? dateTimeProvider.ApplicationNow
            : msg.RejectedAt;

        // TODO: resolve HostCollateralId from a pre-approval master-link when available.
        // For now, stored as null; the export emits a blank CCDCID field for R rows.
        var pending = PendingCollateralResult.Create(
            appraisalId: msg.AppraisalId,
            appraisalNumber: msg.AppraisalNo ?? string.Empty,
            hostCollateralId: null,
            rejectedAt: rejectedAt);

        dbContext.PendingCollateralResults.Add(pending);
        await dbContext.SaveChangesAsync(ct);

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);

        logger.LogInformation(
            "AppraisalRejectedConsumer: spooled PendingCollateralResult {PendingId} for AppraisalId={AppraisalId}",
            pending.Id, msg.AppraisalId);
    }
}
