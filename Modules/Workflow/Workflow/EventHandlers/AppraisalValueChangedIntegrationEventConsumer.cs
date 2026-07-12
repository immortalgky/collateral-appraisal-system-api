using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;
using Workflow.Data;
using Workflow.Meetings.ReadModels;
using Workflow.Workflow.Repositories;

namespace Workflow.EventHandlers;

/// <summary>
/// Consumes AppraisalValueChangedIntegrationEvent from the Appraisal module and writes
/// <c>appraisalValue</c> into WorkflowInstance.Variables so the approval-tier SwitchActivity
/// (meeting vs. direct committee) and the committee-selection ApprovalActivity route on
/// appraised value rather than facility limit.
///
/// Also refreshes AppraisalValue on any live (non-Released) MeetingQueueItem for this appraisal
/// so a queued row's displayed value stays current if pricing changes after enqueue.
///
/// Idempotency mirrors AppointmentDateChangedIntegrationEventConsumer: the InboxMessage INSERT is
/// staged in the same SaveChangesAsync as the work so claim and work never split across commits.
/// </summary>
[ExcludeFromConfigureEndpoints]
public class AppraisalValueChangedIntegrationEventConsumer(
    WorkflowDbContext dbContext,
    IWorkflowInstanceRepository workflowInstanceRepository,
    InboxGuard<WorkflowDbContext> inboxGuard,
    IDateTimeProvider dateTimeProvider,
    ILogger<AppraisalValueChangedIntegrationEventConsumer> logger)
    : IConsumer<AppraisalValueChangedIntegrationEvent>
{
    // Matches InboxGuard.StaleThresholdMinutes so the two share the same reclaim window.
    private const int StaleThresholdMinutes = 5;

    public async Task Consume(ConsumeContext<AppraisalValueChangedIntegrationEvent> context)
    {
        var ct = context.CancellationToken;
        var messageId = context.MessageId;
        var consumerName = GetType().Name;

        // Idempotency check — read-only, no commit yet.
        if (messageId.HasValue)
        {
            var existing = await dbContext.Set<InboxMessage>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MessageId == messageId.Value
                                       && m.ConsumerType == consumerName, ct);

            if (existing?.Status == InboxMessageStatus.Processed)
                return;

            if (existing?.Status == InboxMessageStatus.Processing
                && existing.StartedAt >= dateTimeProvider.ApplicationNow.AddMinutes(-StaleThresholdMinutes))
                return;

            if (existing?.Status == InboxMessageStatus.Processing)
            {
                var schema = dbContext.Model.GetDefaultSchema() ?? "dbo";
                await dbContext.Database.ExecuteSqlRawAsync(
                    "DELETE FROM [" + schema + "].[InboxMessage] " +
                    "WHERE MessageId = {0} AND ConsumerType = {1} AND Status = 'Processing'",
                    new object[] { messageId.Value, consumerName }, ct);
            }
        }

        var msg = context.Message;

        logger.LogInformation(
            "AppraisalValueChanged for AppraisalId={AppraisalId} CorrelationId={CorrelationId} AppraisedValue={AppraisedValue}",
            msg.AppraisalId, msg.CorrelationId, msg.AppraisedValue);

        var instance = await workflowInstanceRepository.GetByCorrelationId(
            msg.CorrelationId.ToString(), ct);

        if (instance is null)
        {
            logger.LogWarning(
                "No workflow instance found for CorrelationId {CorrelationId} — skipping appraisalValue update",
                msg.CorrelationId);
            await CommitAtomicallyAsync(messageId, consumerName, ct);
            await inboxGuard.MarkAsProcessedAsync(messageId, consumerName, ct);
            return;
        }

        instance.UpdateVariables(new Dictionary<string, object>
        {
            ["appraisalValue"] = msg.AppraisedValue
        });

        // Keep any already-queued (not yet Released) meeting-queue row's displayed value fresh.
        var queueItems = await dbContext.MeetingQueueItems
            .Where(q => q.AppraisalId == msg.AppraisalId
                     && q.Status != MeetingQueueItemStatus.Released)
            .ToListAsync(ct);

        foreach (var queueItem in queueItems)
            queueItem.UpdateAppraisalValue(msg.AppraisedValue);

        await CommitAtomicallyAsync(messageId, consumerName, ct);
        await inboxGuard.MarkAsProcessedAsync(messageId, consumerName, ct);
    }

    /// <summary>
    /// Stages an InboxMessage INSERT into the same SaveChangesAsync as any pending entity changes.
    /// On a PK violation (two consumers reached this point), clears the tracker and returns —
    /// the other consumer's commit is authoritative.
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
            dbContext.ChangeTracker.Clear();
        }
    }
}
