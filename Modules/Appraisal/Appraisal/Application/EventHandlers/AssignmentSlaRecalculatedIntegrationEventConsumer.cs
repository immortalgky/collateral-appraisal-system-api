using Appraisal.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Consumes <see cref="AssignmentSlaRecalculatedIntegrationEvent"/> (published by the Workflow
/// module after an appointment-anchored group-window deadline shifts) and re-stamps
/// <c>AppraisalAssignment.SLADueDate</c> via <c>RecalculateSlaDueDate</c>.
///
/// Unlike the first-stamp path (<c>SetSlaDueDate</c> called from
/// <c>WorkflowTransitionedIntegrationEventHandler</c>, which is frozen after the first call),
/// this path is deliberately unconditional — a reschedule must overwrite the prior value.
///
/// Idempotency: the InboxMessage INSERT is staged in the same SaveChangesAsync as the work so
/// claim and mutation never split across separate commits (M4 fix). A PK violation on the inbox
/// INSERT means another consumer committed first — we skip gracefully.
/// Placed on the partitioned "appraisal-sla-recalc" endpoint (Program.cs) so per-appraisal
/// ordering holds; [ExcludeFromConfigureEndpoints] prevents ConfigureEndpoints from also
/// creating an unordered auto-queue for this consumer.
/// </summary>
[ExcludeFromConfigureEndpoints]
public class AssignmentSlaRecalculatedIntegrationEventConsumer(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext,
    InboxGuard<AppraisalDbContext> inboxGuard,
    IDateTimeProvider dateTimeProvider,
    ILogger<AssignmentSlaRecalculatedIntegrationEventConsumer> logger)
    : IConsumer<AssignmentSlaRecalculatedIntegrationEvent>
{
    // Must match InboxGuard.StaleThresholdMinutes so the two share the same reclaim window.
    private const int StaleThresholdMinutes = 5;

    public async Task Consume(ConsumeContext<AssignmentSlaRecalculatedIntegrationEvent> context)
    {
        var ct = context.CancellationToken;
        var messageId = context.MessageId;
        var consumerName = GetType().Name;

        // Read-only idempotency check — no commit yet.
        if (messageId.HasValue)
        {
            var existing = await dbContext.Set<InboxMessage>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MessageId == messageId.Value
                                       && m.ConsumerType == consumerName, ct);

            if (existing?.Status == InboxMessageStatus.Processed)
                return;

            // Processing but still within the live window — another consumer is handling it.
            if (existing?.Status == InboxMessageStatus.Processing
                && existing.StartedAt >= dateTimeProvider.ApplicationNow.AddMinutes(-StaleThresholdMinutes))
                return;

            // Stale Processing row: remove it so the coming INSERT doesn't hit a PK collision.
            if (existing?.Status == InboxMessageStatus.Processing)
            {
                var schema = dbContext.Model.GetDefaultSchema() ?? "dbo";
                await dbContext.Database.ExecuteSqlRawAsync(
                    "DELETE FROM [" + schema + "].[InboxMessage] " +
                    "WHERE MessageId = {0} AND ConsumerType = {1} AND Status = 'Processing'",
                    new object[] { messageId.Value, consumerName }, ct);
            }
        }

        var message = context.Message;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} AppraisalId={AppraisalId} AssignmentId={AssignmentId} NewSlaDueDate={NewSlaDueDate}",
            nameof(AssignmentSlaRecalculatedIntegrationEvent),
            message.AppraisalId,
            message.AssignmentId,
            message.NewSlaDueDate);

        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(message.AppraisalId, ct);

        if (appraisal is null)
        {
            logger.LogWarning(
                "Appraisal {AppraisalId} not found when handling {IntegrationEvent} — skipping",
                message.AppraisalId, nameof(AssignmentSlaRecalculatedIntegrationEvent));
            await CommitAtomicallyAsync(messageId, consumerName, ct);
            await inboxGuard.MarkAsProcessedAsync(messageId, consumerName, ct);
            return;
        }

        var assignment = appraisal.Assignments
            .FirstOrDefault(a => a.Id == message.AssignmentId);

        if (assignment is null)
        {
            logger.LogWarning(
                "Assignment {AssignmentId} not found on Appraisal {AppraisalId} — skipping",
                message.AssignmentId, message.AppraisalId);
            await CommitAtomicallyAsync(messageId, consumerName, ct);
            await inboxGuard.MarkAsProcessedAsync(messageId, consumerName, ct);
            return;
        }

        assignment.RecalculateSlaDueDate(message.NewSlaDueDate);

        // Commit mutation + inbox INSERT atomically. If SaveChangesAsync throws (transient DB error),
        // neither is committed and MassTransit will retry cleanly — no orphaned Processing row.
        await CommitAtomicallyAsync(messageId, consumerName, ct);
        await inboxGuard.MarkAsProcessedAsync(messageId, consumerName, ct);

        logger.LogInformation(
            "RecalculateSlaDueDate applied: AppraisalId={AppraisalId} AssignmentId={AssignmentId} SLADueDate={SLADueDate}",
            message.AppraisalId, message.AssignmentId, message.NewSlaDueDate);
    }

    /// <summary>
    /// Stages an InboxMessage INSERT into the same SaveChangesAsync as any pending entity changes.
    /// On a PK violation (race: two consumers reached this point), clears the tracker and returns
    /// — the other consumer's commit is the authoritative one.
    /// </summary>
    private async Task CommitAtomicallyAsync(Guid? messageId, string consumerName, CancellationToken ct)
    {
        if (messageId.HasValue)
        {
            dbContext.Set<InboxMessage>().Add(
                InboxMessage.Create(messageId.Value, consumerName, dateTimeProvider.ApplicationNow));
        }

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // PK violation on InboxMessage INSERT: another consumer committed the same message
            // between our read and our save — idempotent skip.
            dbContext.ChangeTracker.Clear();
        }
    }
}
