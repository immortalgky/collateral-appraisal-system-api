using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;
using Workflow.Data;
using Workflow.Sla.Models;
using Workflow.Sla.Services;
using Workflow.Workflow.Repositories;

namespace Workflow.EventHandlers;

/// <summary>
/// Consumes AppointmentDateChangedIntegrationEvent from the Appraisal module and:
/// (a) writes "appointmentDate" into WorkflowInstance.Variables so the next TaskActivity
///     execution uses it as the SLA anchor, and
/// (b) recomputes PendingTask.DueAt for any currently-active appointment-anchored task
///     (AnchorType = AppointmentDate SlaPolicy) — a reschedule must move the deadline.
///
/// Idempotency: the InboxMessage INSERT is staged in the same SaveChangesAsync as the work
/// so claim and work never split across separate commits (M4 fix). A PK violation on the
/// inbox INSERT means another consumer committed first — we skip gracefully.
/// </summary>
[ExcludeFromConfigureEndpoints]
public class AppointmentDateChangedIntegrationEventConsumer(
    WorkflowDbContext dbContext,
    IWorkflowInstanceRepository workflowInstanceRepository,
    ISlaCalculator slaCalculator,
    InboxGuard<WorkflowDbContext> inboxGuard,
    IDateTimeProvider dateTimeProvider,
    IIntegrationEventOutbox outbox,
    ILogger<AppointmentDateChangedIntegrationEventConsumer> logger)
    : IConsumer<AppointmentDateChangedIntegrationEvent>
{
    // Matches InboxGuard.StaleThresholdMinutes so the two share the same reclaim window.
    private const int StaleThresholdMinutes = 5;

    public async Task Consume(ConsumeContext<AppointmentDateChangedIntegrationEvent> context)
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

            // Processing but still within the live window — another consumer is handling it.
            if (existing?.Status == InboxMessageStatus.Processing
                && existing.StartedAt >= dateTimeProvider.ApplicationNow.AddMinutes(-StaleThresholdMinutes))
                return;

            // Stale Processing row: remove it so our coming INSERT doesn't hit a PK collision.
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
            "AppointmentDateChanged for AppraisalId={AppraisalId} CorrelationId={CorrelationId} AppointmentDate={AppointmentDate}",
            msg.AppraisalId, msg.CorrelationId, msg.AppointmentDate);

        // (a) Find the workflow instance and write the updated appointmentDate variable.
        var instance = await workflowInstanceRepository.GetByCorrelationId(
            msg.CorrelationId.ToString(), ct);

        if (instance is null)
        {
            logger.LogWarning(
                "No workflow instance found for CorrelationId {CorrelationId} — skipping appointment variable update",
                msg.CorrelationId);
            // No meaningful work to do — stage an inbox record to acknowledge and exit.
            await CommitAtomicallyAsync(messageId, consumerName, ct);
            await inboxGuard.MarkAsProcessedAsync(messageId, consumerName, ct);
            return;
        }

        instance.UpdateVariables(new Dictionary<string, object>
        {
            ["appointmentDate"] = msg.AppointmentDate
        });

        // (b) Recompute appointment-anchored deadlines for active tasks on this instance — both
        // Activity-scope appointment-anchored tasks AND tasks governed by an appointment-anchored
        // group window. Assignment-anchored tasks are appointment-independent and left untouched, so a
        // reschedule never resets a legitimately at-risk/breached clock.
        var anchoredActivityIds = await dbContext.SlaPolicies
            .AsNoTracking()
            .Where(p => p.Scope == SlaPolicyScope.Activity
                     && p.AnchorType == SlaAnchorType.AppointmentDate)
            .Select(p => p.ActivityId)
            .Distinct()
            .ToListAsync(ct);

        var pendingTasks = await dbContext.PendingTasks
            .Where(t => t.WorkflowInstanceId == instance.Id)
            .ToListAsync(ct);

        // Captured from the first window-governed appointment-anchored task; used after the loop
        // to publish AssignmentSlaRecalculatedIntegrationEvent so the Appraisal module can
        // re-stamp AppraisalAssignment.SLADueDate unconditionally (via RecalculateSlaDueDate).
        DateTime? capturedAssignmentDueAt = null;

        if (pendingTasks.Count > 0)
        {
            var variables = instance.Variables;

            var companyId = variables.TryGetValue("assignedCompanyId", out var cid)
                            && Guid.TryParse(cid?.ToString(), out var parsedCid)
                ? parsedCid
                : (Guid?)null;

            var loanType = variables.TryGetValue("bankingSegment", out var seg)
                           && !string.IsNullOrWhiteSpace(seg?.ToString())
                ? seg!.ToString()
                : null;

            var appraisalType = variables.TryGetValue("appraisalType", out var at)
                ? at?.ToString()
                : null;

            foreach (var task in pendingTasks)
            {
                // Window-governed member task: recompute only when the window is appointment-anchored.
                var governing = await slaCalculator.ResolveGoverningStageDueAtAsync(
                    activityId: task.ActivityId,
                    workflowDefinitionId: instance.WorkflowDefinitionId,
                    companyId: companyId,
                    loanType: loanType,
                    appraisalType: appraisalType,
                    assignedAt: task.AssignedAt,
                    correlationId: msg.CorrelationId,
                    appointmentDate: msg.AppointmentDate,
                    ct: ct);

                if (governing is not null)
                {
                    if (governing.AnchorType == SlaAnchorType.AppointmentDate)
                    {
                        task.RecalculateDueAt(governing.DueAt, governing.StartAt, governing.DurationHours);
                        logger.LogInformation(
                            "Recalculated window-governed PendingTask DueAt for activity {ActivityId}: {DueAt}",
                            task.ActivityId, governing.DueAt);

                        // All member tasks in the same window share the same governing DueAt.
                        // Capture the first non-null value so we can notify the Appraisal module
                        // to re-stamp AppraisalAssignment.SLADueDate after the loop.
                        capturedAssignmentDueAt ??= governing.DueAt;
                    }

                    continue; // a window governs this task — never fall through to the per-activity calc
                }

                // Activity-scope appointment-anchored task.
                if (!anchoredActivityIds.Contains(task.ActivityId))
                    continue;

                var deadline = await slaCalculator.CalculateActivityDueAtAsync(
                    activityId: task.ActivityId,
                    workflowDefinitionId: instance.WorkflowDefinitionId,
                    companyId: companyId,
                    loanType: loanType,
                    appraisalType: appraisalType,
                    assignedAt: task.AssignedAt,
                    defaultTimeout: null,
                    workflowDueAt: instance.WorkflowDueAt,
                    correlationId: msg.CorrelationId,
                    appointmentDate: msg.AppointmentDate,
                    ct: ct);

                task.RecalculateDueAt(deadline.DueAt, deadline.StartAt, deadline.DurationHours);

                logger.LogInformation(
                    "Recalculated PendingTask DueAt for activity {ActivityId}: {DueAt}",
                    task.ActivityId, deadline.DueAt);
            }
        }

        // Notify the Appraisal module that the assignment-level SLA deadline must shift.
        // Published via the outbox so it is committed atomically with the workflow work
        // (the DispatchDomainEventInterceptor drains outbox messages into the same SaveChangesAsync).
        // Only published when a window-governed appointment-anchored task was recalculated AND
        // the triggering event carries a non-empty AssignmentId to target.
        if (capturedAssignmentDueAt.HasValue && msg.AssignmentId != Guid.Empty)
        {
            outbox.Publish(new AssignmentSlaRecalculatedIntegrationEvent
            {
                AppraisalId    = msg.AppraisalId,
                AssignmentId   = msg.AssignmentId,
                NewSlaDueDate  = capturedAssignmentDueAt.Value,
                OccurredOn     = dateTimeProvider.ApplicationNow,
            });

            logger.LogInformation(
                "Staged AssignmentSlaRecalculatedIntegrationEvent for AssignmentId={AssignmentId} NewSlaDueDate={NewSlaDueDate}",
                msg.AssignmentId, capturedAssignmentDueAt.Value);
        }

        // Commit work + inbox INSERT atomically.  If SaveChangesAsync throws (transient DB error),
        // neither is committed and MassTransit will retry cleanly — no "Processing" row to block it.
        await CommitAtomicallyAsync(messageId, consumerName, ct);
        await inboxGuard.MarkAsProcessedAsync(messageId, consumerName, ct);
    }

    /// <summary>
    /// Stages an InboxMessage INSERT into the same SaveChangesAsync as any pending entity changes.
    /// On a PK violation (race condition: two consumers reached this point), clears the tracker
    /// and returns — the other consumer's commit is the authoritative one.
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
