using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Maps workflow activity transitions to Appraisal status AND active-AppraisalAssignment status.
///
/// Appraisal-status mapping mirrors the semantics of vw_AppraisalList.sql so that the persisted
/// status stays in sync with the workflow graph.
///
/// Assignment-status mapping drives the path-aware lifecycle (Pending → Assigned → InProgress →
/// UnderReview → Verified → Completed). The Pending → Assigned transition is owned synchronously
/// by the engagement command handler (so the admin screen locks immediately), and the terminal
/// Completed transition is owned by the committee-approval handler — both are intentionally absent
/// from the mapping below to preserve single-owner semantics for those state changes.
///
/// Terminal transitions (DestinationActivityId == null) are intentional no-ops here;
/// MarkApprovedByCommittee / Cancel own those transitions via their dedicated events.
///
/// Wired to the partitioned "appraisal-sync" endpoint in Program.cs
/// (per-AppraisalId ordering across the cluster), so [ExcludeFromConfigureEndpoints] keeps
/// ConfigureEndpoints from also creating a default unordered queue.
/// </summary>
[ExcludeFromConfigureEndpoints]
public class WorkflowTransitionedIntegrationEventHandler(
    ILogger<WorkflowTransitionedIntegrationEventHandler> logger,
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork,
    AppraisalDbContext dbContext,
    InboxGuard<AppraisalDbContext> inboxGuard,
    ISlaCalculatorClient slaCalculatorClient)
    : IConsumer<WorkflowTransitionedIntegrationEvent>
{
    // Tier 1: status depends on whether an active assignment exists.
    // No active assignment → Submitted (RM/checker maker-checker stage).
    // Active assignment exists (routed back from in-progress work) → InProgress.
    private static readonly HashSet<string> AssignmentDependentActivities =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "appraisal-initiation",
            "appraisal-initiation-check",
        };

    // Tier 2: fixed appraisal status regardless of assignment state.
    private static readonly IReadOnlyDictionary<string, AppraisalStatus> ActivityStatusMap =
        new Dictionary<string, AppraisalStatus>(StringComparer.OrdinalIgnoreCase)
        {
            ["appraisal-assignment"]        = AppraisalStatus.Pending,
            ["ext-appraisal-assignment"]    = AppraisalStatus.InProgress,
            ["ext-appraisal-execution"]     = AppraisalStatus.InProgress,
            ["ext-appraisal-check"]         = AppraisalStatus.InProgress,
            ["ext-appraisal-verification"]  = AppraisalStatus.InProgress,
            ["int-appraisal-execution"]     = AppraisalStatus.InProgress,
            ["int-appraisal-check"]         = AppraisalStatus.UnderReview,
            ["int-appraisal-verification"]  = AppraisalStatus.UnderReview,
            ["appraisal-book-verification"] = AppraisalStatus.UnderReview,
            ["pending-meeting"]             = AppraisalStatus.UnderReview,
            ["pending-approval"]            = AppraisalStatus.UnderReview,
        };

    // Bank-review activities — the set of "bank is examining the work" workflow stages. A
    // transition whose source is one of these and whose destination loops back to the appraiser
    // is a routeback (rework). int-appraisal-check is included so the Internal-path edge
    // (int-check → int-execution, route_back && Internal) is correctly classified as rework.
    private static readonly HashSet<string> BankReviewActivities =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "appraisal-book-verification",
            "int-appraisal-check",
            "int-appraisal-verification",
            "pending-meeting",
            "pending-approval",
        };

    public async Task Consume(ConsumeContext<WorkflowTransitionedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;
        var ct = context.CancellationToken;

        // Terminal workflow completion or non-appraisal workflow — nothing to do.
        if (message.AppraisalId is null || message.DestinationActivityId is null)
        {
            logger.LogDebug(
                "WorkflowTransitionedIntegrationEvent skipped: AppraisalId={AppraisalId} DestinationActivityId={DestinationActivityId}",
                message.AppraisalId, message.DestinationActivityId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for AppraisalId: {AppraisalId} Source: {Source} Destination: {Destination}",
            nameof(WorkflowTransitionedIntegrationEvent),
            message.AppraisalId,
            message.SourceActivityId,
            message.DestinationActivityId);

        try
        {
            var isTier1 = AssignmentDependentActivities.Contains(message.DestinationActivityId);
            ActivityStatusMap.TryGetValue(message.DestinationActivityId, out var staticTarget);
            var isTier2 = !isTier1 && staticTarget is not null;
            var hasAppraisalStatusMapping = isTier1 || isTier2;

            // Always load with all data so the active assignment is available for the assignment
            // mapping below; the cost is negligible compared to the cross-module integration churn.
            var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(message.AppraisalId.Value, ct);

            if (appraisal is null)
            {
                logger.LogWarning(
                    "Appraisal {AppraisalId} not found when handling {IntegrationEvent}",
                    message.AppraisalId,
                    nameof(WorkflowTransitionedIntegrationEvent));
                await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
                return;
            }

            var appraisalStatusChanged = false;
            if (hasAppraisalStatusMapping)
            {
                var target = isTier1 ? AppraisalStatus.InProgress : staticTarget!;
                appraisal.SyncStatusFromWorkflow(target);
                appraisalStatusChanged = true;
            }

            var assignmentStatusChanged = TryApplyAssignmentTransition(
                appraisal,
                message.SourceActivityId,
                message.DestinationActivityId,
                message.CompletedAt);

            if (!appraisalStatusChanged && !assignmentStatusChanged)
            {
                logger.LogDebug(
                    "WorkflowTransitionedIntegrationEvent: no mapping for transition {Source} → {Destination}, skipping",
                    message.SourceActivityId, message.DestinationActivityId);
                await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
                return;
            }

            // Stamp the assignment-level SLA DueDate when the workflow enters a stage-start activity.
            // The resolver returns null when no Stage-scope SlaPolicy matches the destination activity,
            // so no hardcoded activity keys are needed here.
            var activeAssignment = appraisal.Assignments
                .Where(a => a.AssignmentStatus != AssignmentStatus.Completed
                         && a.AssignmentStatus != AssignmentStatus.Rejected
                         && a.AssignmentStatus != AssignmentStatus.Cancelled)
                .OrderByDescending(a => a.AssignedAt)
                .ThenByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .FirstOrDefault();

            if (activeAssignment is not null
                && message.DestinationActivityId is not null
                && activeAssignment.AssignmentStatus != AssignmentStatus.Pending   // M4: skip pre-stage transitions
                && activeAssignment.SLADueDate is null)
            {
                var companyId = activeAssignment.AssigneeCompanyId is not null
                    && Guid.TryParse(activeAssignment.AssigneeCompanyId, out var cid) ? cid : (Guid?)null;

                // M3: WorkflowDefinitionId is now carried by the event. Pass null when absent so
                // only wildcard Stage policies (WorkflowDefinitionId = null) match.
                // The SLA "loan type" axis carries banking-segment values, so the appraisal's segment
                // and type scope the per-stage (group) OLA policy lookup.
                // Pass CorrelationId so the calculator can subtract cumulative consumed time
                // from prior stage executions (rework does not grant a fresh full budget).
                // Supply the assignment's appointment so an AppointmentDate-anchored window can stamp from
                // the visit; the calc branches on the resolved policy's AnchorType, so Assignment-anchored
                // windows ignore it and keep stamping from startedAt.
                // Use only confirmed appointments — "Pending" (awaiting approval) must not
                // anchor an SLA stamp, because the date may still change before approval.
                var appointmentDate = await dbContext.Appointments
                    .AsNoTracking()
                    .Where(a => a.AssignmentId == activeAssignment.Id && a.Status == "Appointed")
                    .OrderByDescending(a => a.AppointmentDateTime)
                    .Select(a => (DateTime?)a.AppointmentDateTime)
                    .FirstOrDefaultAsync(ct);

                var stageDueAt = await slaCalculatorClient.GetStageDueAtAsync(
                    workflowDefinitionId: message.WorkflowDefinitionId,
                    startActivityKey: message.DestinationActivityId,
                    startedAt: message.CompletedAt,
                    companyId: companyId,
                    loanType: string.IsNullOrWhiteSpace(appraisal.BankingSegment) ? null : appraisal.BankingSegment,
                    appraisalType: appraisal.AppraisalType,
                    correlationId: message.CorrelationId,
                    appointmentDate: appointmentDate,
                    ct: ct);

                if (stageDueAt.HasValue)
                    activeAssignment.SetSlaDueDate(stageDueAt.Value);
            }

            await unitOfWork.SaveChangesAsync(ct);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);

            logger.LogInformation(
                "Successfully processed workflow transition for AppraisalId {AppraisalId} ({Source} → {Destination}); appraisalStatusChanged={AppraisalChanged} assignmentStatusChanged={AssignmentChanged}",
                message.AppraisalId,
                message.SourceActivityId,
                message.DestinationActivityId,
                appraisalStatusChanged,
                assignmentStatusChanged);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing {IntegrationEvent} for AppraisalId: {AppraisalId}",
                nameof(WorkflowTransitionedIntegrationEvent),
                message.AppraisalId);

            throw;
        }
    }

    /// <summary>
    /// Apply the assignment-status transition for the given workflow edge. Returns true if any
    /// assignment was mutated. Idempotent: re-running the same edge on an already-transitioned
    /// assignment is a no-op (each domain method either short-circuits or guards on current state).
    /// </summary>
    private bool TryApplyAssignmentTransition(
        AppraisalAggregate appraisal,
        string? source,
        string destination,
        DateTime occurredAt)
    {
        var assignment = appraisal.Assignments
            .Where(a => a.AssignmentStatus != AssignmentStatus.Completed
                     && a.AssignmentStatus != AssignmentStatus.Rejected
                     && a.AssignmentStatus != AssignmentStatus.Cancelled)
            .OrderByDescending(a => a.AssignedAt)
            .ThenByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .FirstOrDefault();
        if (assignment is null) return false;

        // ─── Rework edges (heavy: appraiser must redo work) ─────────────────────────────────
        // External: bank pulls all the way back to the company. Two flavours per appraisal-workflow.json:
        //   - book-verification → ext-appraisal-assignment ("route_back_ext_admin")
        //   - book-verification → ext-appraisal-check       ("route_back_ext_checker")
        // Internal: bank pulls back to the in-house appraiser. Possible from int-check, meeting,
        // or approval all the way back to int-appraisal-execution.
        var isExternalRework = source is not null
            && BankReviewActivities.Contains(source)
            && (string.Equals(destination, "ext-appraisal-assignment", StringComparison.OrdinalIgnoreCase)
             || string.Equals(destination, "ext-appraisal-check", StringComparison.OrdinalIgnoreCase));

        var isInternalRework = source is not null
            && BankReviewActivities.Contains(source)
            && string.Equals(destination, "int-appraisal-execution", StringComparison.OrdinalIgnoreCase);

        if (isExternalRework || isInternalRework)
        {
            if (assignment.AssignmentStatus == AssignmentStatus.UnderReview
                || assignment.AssignmentStatus == AssignmentStatus.Verified)
            {
                assignment.Rework("Routed back from bank review", occurredAt);
                return true;
            }
            return false;
        }

        // ─── Verified-demote edges (light: bank reverses its own acceptance, appraiser idle) ─
        // External-only: meeting / approval routing back to appraisal-book-verification means the
        // bank is re-examining the book it had already verified. The company didn't resubmit, so
        // we demote Verified → UnderReview rather than Rework. This closes the invoicing-gate
        // hole where a previously-Verified assignment would otherwise stay invoice-eligible while
        // the bank is actively reconsidering.
        if (source is not null
            && (string.Equals(source, "pending-meeting", StringComparison.OrdinalIgnoreCase)
             || string.Equals(source, "pending-approval", StringComparison.OrdinalIgnoreCase))
            && string.Equals(destination, "appraisal-book-verification", StringComparison.OrdinalIgnoreCase))
        {
            if (assignment.AssignmentStatus == AssignmentStatus.Verified)
            {
                assignment.MarkUnderReview();
                return true;
            }
            return false;
        }

        // ─── Verified gate ──────────────────────────────────────────────────────────────────
        // Bank verifier accepted: int-appraisal-verification → approval-tier-switch is the only
        // edge that leaves int-verification toward downstream approval. The other int-verification
        // exits (route_back to int-check, never to ext-assignment in the current workflow) are
        // bank-internal cycles that must NOT set Verified.
        if (string.Equals(source, "int-appraisal-verification", StringComparison.OrdinalIgnoreCase)
            && string.Equals(destination, "approval-tier-switch", StringComparison.OrdinalIgnoreCase))
        {
            if (assignment.AssignmentStatus == AssignmentStatus.UnderReview)
            {
                assignment.MarkVerified();
                return true;
            }
            return false;
        }

        // ─── Destination-driven transitions on the active assignment ────────────────────────
        switch (destination.ToLowerInvariant())
        {
            case "ext-appraisal-execution":
            case "int-appraisal-execution":
                // INT: InternalAssignedIntegrationEventHandler now owns the Assigned→InProgress
                // transition (assign and start coincide on int-appraisal-execution), so this case is a
                // no-op for internal once that has run. It remains authoritative for the EXT path,
                // where Assigned is stamped much earlier (at company-selection) with no race.
                if (assignment.AssignmentStatus == AssignmentStatus.Assigned)
                {
                    assignment.StartWork();
                    return true;
                }
                return false;

            case "appraisal-book-verification":
                // External handoff: company finished its own QC, bank now reviews the book.
                if (assignment.AssignmentStatus == AssignmentStatus.InProgress)
                {
                    assignment.MarkUnderReview();
                    return true;
                }
                return false;

            case "int-appraisal-check":
                // Internal handoff: in-house appraiser finished, in-house checker starts review.
                if (assignment.AssignmentStatus == AssignmentStatus.InProgress)
                {
                    assignment.MarkUnderReview();
                    return true;
                }
                return false;

            default:
                return false;
        }
    }
}
