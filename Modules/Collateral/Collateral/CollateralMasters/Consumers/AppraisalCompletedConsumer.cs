using MassTransit;
using Shared.Exceptions;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Collateral.CollateralMasters.Consumers;

/// <summary>
/// Listens for AppraisalCompletedIntegrationEvent and triggers the CollateralMaster upsert.
/// Exceptions propagate so MassTransit retries (5x exponential) then dead-letters.
/// `ConflictException` (multi-title overlap) is logged with structured context — retrying
/// won't help since the data won't change without admin merge — and configured to skip retry
/// in MassTransitConfiguration via UseMessageRetry.Ignore<ConflictException>().
/// Auto-registered via the collateralAssembly scan in Program.cs.
/// InboxGuard deduplicates concurrent/retried delivery so last-known field updates are not
/// silently lost on second-receipt unique-violation paths.
/// </summary>
public class AppraisalCompletedConsumer(
    ICollateralMasterUpsertService upsertService,
    ILogger<AppraisalCompletedConsumer> logger,
    InboxGuard<CollateralDbContext> inboxGuard)
    : IConsumer<AppraisalCompletedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalCompletedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var msg = context.Message;

        logger.LogInformation(
            "AppraisalCompletedConsumer: processing AppraisalId={AppraisalId}",
            msg.AppraisalId);

        try
        {
            await upsertService.ProcessAppraisalAsync(msg.AppraisalId, context.CancellationToken);
        }
        catch (ConflictException ex)
        {
            // Multi-title overlap: the appraisal's titles span multiple existing CollateralMaster
            // groups. Retrying won't help — admin must merge the masters before this can succeed.
            // Logged with high severity so ops can find it; rethrown so MassTransit dead-letters
            // (UseMessageRetry should be configured to ignore this exception type).
            logger.LogError(ex,
                "AppraisalCompletedConsumer: ConflictException for AppraisalId={AppraisalId} — admin merge required. " +
                "Message will be dead-lettered without retry.",
                msg.AppraisalId);
            throw;
        }

        logger.LogInformation(
            "AppraisalCompletedConsumer: completed for AppraisalId={AppraisalId}",
            msg.AppraisalId);

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
