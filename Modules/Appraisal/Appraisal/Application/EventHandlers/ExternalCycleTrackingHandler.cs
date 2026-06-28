using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.Contracts.Sla;

namespace Appraisal.Application.EventHandlers;

// Wired to the partitioned "appraisal-ext-cycle" endpoint in Program.cs so
// open/close cycle transitions for one appraisal are processed in order across the cluster.
[ExcludeFromConfigureEndpoints]
public class ExternalCycleTrackingHandler(
    AppraisalDbContext dbContext,
    IBusinessTimeCalculator businessTimeCalculator,
    IIntegrationEventOutbox outbox,
    ILogger<ExternalCycleTrackingHandler> logger,
    InboxGuard<AppraisalDbContext> inboxGuard)
    : IConsumer<WorkflowTransitionedIntegrationEvent>
{
    // All workflow activities that belong to the external company engagement loop.
    // isOpen = source NOT in this set AND destination IN this set — covers initial routing and
    // all routeback edges (book-verification-back-to-ext-admin, book-verification-back-to-ext-checker, …).
    private static readonly HashSet<string> ExtActivities = new(StringComparer.OrdinalIgnoreCase)
    {
        "ext-appraisal-assignment",
        "ext-appraisal-execution",
        "ext-appraisal-check",
        "ext-appraisal-verification"
    };

    private const string ExtVerificationActivity = "ext-appraisal-verification";
    private const string BookVerificationActivity = "appraisal-book-verification";

    public async Task Consume(ConsumeContext<WorkflowTransitionedIntegrationEvent> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        // Open: any transition from outside ext-* INTO ext-* (handles all routeback edges, not just ext-assignment).
        var isOpen = !ExtActivities.Contains(message.SourceActivityId ?? "")
                     && ExtActivities.Contains(message.DestinationActivityId ?? "");

        // Close: the single authoritative exit — ext-appraisal-verification → appraisal-book-verification.
        var isClose = string.Equals(message.SourceActivityId, ExtVerificationActivity, StringComparison.OrdinalIgnoreCase)
                      && string.Equals(message.DestinationActivityId, BookVerificationActivity, StringComparison.OrdinalIgnoreCase);

        if (!isOpen && !isClose) return;
        if (message.AppraisalId is null) return;

        // Per-AppraisalId ordering is enforced by the partitioned
        // "appraisal-ext-cycle" endpoint (Program.cs). The defensive close-before-open and
        // idempotent OpenExternalCycle remain as belt-and-suspenders.
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, ct))
            return;

        try
        {
            if (isOpen)
                await HandleOpenAsync(message, ct);
            else
                await HandleCloseAsync(message, ct);

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error in ExternalCycleTrackingHandler for AppraisalId {AppraisalId} Source={Source} Dest={Dest}",
                message.AppraisalId, message.SourceActivityId, message.DestinationActivityId);
            throw;
        }
    }

    private async Task HandleOpenAsync(WorkflowTransitionedIntegrationEvent message, CancellationToken ct)
    {
        var assignment = await LoadActiveExternalAssignmentAsync(message.AppraisalId!.Value, includeCycles: true, ct);
        if (assignment is null)
        {
            logger.LogWarning(
                "ExternalCycleTrackingHandler: no active external assignment for AppraisalId {AppraisalId} on open",
                message.AppraisalId);
            return;
        }

        assignment.OpenExternalCycle(message.CompletedAt);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "ExternalCycleTrackingHandler: opened cycle for AssignmentId {AssignmentId} AppraisalId {AppraisalId}",
            assignment.Id, message.AppraisalId);
    }

    private async Task HandleCloseAsync(WorkflowTransitionedIntegrationEvent message, CancellationToken ct)
    {
        var assignment = await LoadActiveExternalAssignmentAsync(message.AppraisalId!.Value, includeCycles: true, ct);
        if (assignment is null)
        {
            logger.LogWarning(
                "ExternalCycleTrackingHandler: no active external assignment for AppraisalId {AppraisalId} on close",
                message.AppraisalId);
            return;
        }

        // FirstOrDefault ordered by CycleNumber DESC — defensive against any accidental double-open.
        var openCycle = assignment.Cycles.OrderByDescending(c => c.CycleNumber).FirstOrDefault(c => c.Status == CycleStatus.Open);
        if (openCycle is null)
        {
            logger.LogWarning(
                "ExternalCycleTrackingHandler: no open cycle found for AssignmentId {AssignmentId} on close — skipping",
                assignment.Id);
            return;
        }

        var businessMinutes = await businessTimeCalculator.GetBusinessMinutesBetweenAsync(
            openCycle.OpenedAt, message.CompletedAt, ct);

        assignment.CloseLatestOpenCycle(message.CompletedAt, businessMinutes);

        // Validate AssigneeCompanyId before publishing. On failure, still persist the cycle close
        // to avoid a poison-loop; the missing event is a business concern, not a processing error.
        if (!Guid.TryParse(assignment.AssigneeCompanyId, out var companyId))
        {
            logger.LogWarning(
                "ExternalCycleTrackingHandler: AssigneeCompanyId '{RawValue}' is not a valid Guid for AssignmentId {AssignmentId} — cycle closed but event not published",
                assignment.AssigneeCompanyId, assignment.Id);

            await dbContext.SaveChangesAsync(ct);
            return;
        }

        // Enqueue into the outbox atomically with the cycle-close save (feedback_cross_module_outbox).
        outbox.Publish(new ExternalAppraisalReturnedIntegrationEvent
        {
            AppraisalId = message.AppraisalId!.Value,
            AssignmentId = assignment.Id,
            CompanyId = companyId,
            CycleNumber = openCycle.CycleNumber,
            OpenedAt = openCycle.OpenedAt,
            ClosedAt = message.CompletedAt,
            BusinessMinutes = businessMinutes
        }, message.AppraisalId!.Value.ToString());

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "ExternalCycleTrackingHandler: closed cycle {CycleNumber} for AssignmentId {AssignmentId}, BusinessMinutes={Minutes}",
            openCycle.CycleNumber, assignment.Id, businessMinutes);
    }

    private async Task<AppraisalAssignment?> LoadActiveExternalAssignmentAsync(
        Guid appraisalId,
        bool includeCycles,
        CancellationToken ct)
    {
        // Filter after materialization: AssignmentType / AssignmentStatus are HasConversion value
        // objects and EF cannot translate value-object equality to SQL. Row count per appraisal is tiny.
        var query = dbContext.AppraisalAssignments
            .Where(a => a.AppraisalId == appraisalId)
            .OrderByDescending(a => a.AssignedAt)
            .ThenByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id);

        var rows = includeCycles
            ? await query.Include(a => a.Cycles).ToListAsync(ct)
            : await query.ToListAsync(ct);

        return rows.FirstOrDefault(a => a.AssignmentType == AssignmentType.External
                                        && a.AssignmentStatus != AssignmentStatus.Rejected
                                        && a.AssignmentStatus != AssignmentStatus.Cancelled);
    }
}
