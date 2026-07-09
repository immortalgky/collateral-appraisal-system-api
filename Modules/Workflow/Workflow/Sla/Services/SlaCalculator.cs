using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Workflow.Contracts.Sla;
using Workflow.Data;
using Workflow.Sla.Models;

namespace Workflow.Sla.Services;

public class SlaCalculator(
    WorkflowDbContext dbContext,
    IBusinessTimeCalculator businessTimeCalculator,
    ILogger<SlaCalculator> logger) : ISlaCalculator
{
    /// <remarks>
    /// <paramref name="workflowDefinitionId"/> is non-nullable here because Activity-scope policies
    /// must always be resolved within a known workflow context — callers should never need to search
    /// across all workflow definitions for an activity. This is intentionally asymmetric with
    /// <see cref="CalculateStageDueAtAsync"/>, which accepts a nullable workflowDefinitionId to
    /// support global Stage policies that apply regardless of workflow definition.
    /// </remarks>
    public async Task<SlaDeadline> CalculateActivityDueAtAsync(
        string activityId,
        Guid workflowDefinitionId,
        Guid? companyId,
        string? loanType,
        string? appraisalType,
        DateTime assignedAt,
        TimeSpan? defaultTimeout,
        DateTime? workflowDueAt,
        Guid? correlationId = null,
        DateTime? appointmentDate = null,
        CancellationToken ct = default)
    {
        // 1. Look up SLA policy (ordered by priority, lower wins). Activity scope only.
        var policy = await dbContext.SlaPolicies
            .AsNoTracking()
            .Where(s => s.Scope == SlaPolicyScope.Activity)
            .Where(s => s.ActivityId == activityId || s.ActivityId == "*")
            .Where(s => s.WorkflowDefinitionId == null || s.WorkflowDefinitionId == workflowDefinitionId)
            .Where(s => s.CompanyId == null || s.CompanyId == companyId)
            .Where(s => s.LoanType == null || s.LoanType == loanType)
            .Where(s => s.AppraisalType == null || s.AppraisalType == appraisalType)
            // Priority wins; ties broken toward the more specific row so a wildcard default and a
            // specific override can safely share a priority (the widened unique index allows this).
            .OrderBy(s => s.Priority)
            .ThenByDescending(s => s.ActivityId == activityId)
            .ThenByDescending(s => s.LoanType != null)
            .ThenByDescending(s => s.AppraisalType != null)
            .FirstOrDefaultAsync(ct);

        int? durationHours = policy?.DurationHours;
        bool useBusinessDays = policy?.UseBusinessDays ?? false;

        // 2. Fall back to workflow JSON timeoutDuration
        if (durationHours is null && defaultTimeout.HasValue)
        {
            durationHours = (int)defaultTimeout.Value.TotalHours;
            // Treat the JSON timeoutDuration as BUSINESS hours, consistent with the seeded
            // per-activity policies (which mirror the same hour numbers as business hours).
            // So a fallback activity (e.g. pending-approval PT72H) skips weekends/lunch/holidays
            // exactly like an explicitly-seeded one, instead of counting straight calendar time.
            useBusinessDays = true;
        }

        if (durationHours is null)
        {
            logger.LogDebug("No SLA policy found for activity {ActivityId}, skipping", activityId);
            return new SlaDeadline(null, null);
        }

        // 3. Determine anchor: AppointmentDate-anchored policies start the clock from the visit, not AssignedAt.
        var effectiveAnchorType = policy?.AnchorType ?? SlaAnchorType.Assignment;
        DateTime? anchor;
        if (effectiveAnchorType == SlaAnchorType.AppointmentDate)
        {
            if (appointmentDate is null)
            {
                logger.LogDebug(
                    "Activity {ActivityId} is appointment-anchored but no appointment date is set — DueAt deferred",
                    activityId);
                // DueAt is deferred until the appointment is set, but the budget is already resolved —
                // carry it so the task can display its SLA policy before a deadline exists.
                return new SlaDeadline(null, null, durationHours);
            }
            anchor = appointmentDate.Value;
        }
        else
        {
            anchor = assignedAt;
        }

        // 4. Subtract cumulative business-time already consumed by prior executions (rework).
        // This means reworked tasks start from the REMAINING budget, not a fresh window.
        int cumulativeMinutes = 0;
        if (correlationId.HasValue)
        {
            var priorLegs = await dbContext.CompletedTasks
                .AsNoTracking()
                .Where(t => t.CorrelationId == correlationId.Value
                         && t.ActivityId == activityId
                         && t.ActionTaken != "Reassigned")
                .Select(t => new { t.AssignedAt, t.CompletedAt })
                .ToListAsync(ct);

            foreach (var leg in priorLegs)
            {
                cumulativeMinutes += await businessTimeCalculator.GetBusinessMinutesBetweenAsync(
                    leg.AssignedAt, leg.CompletedAt, ct);
            }
        }

        // 5. Keep arithmetic in minutes to avoid integer-division truncation (e.g. 70 min consumed
        // on a 72h budget should leave 4250 min remaining, not 71h if we divided first).
        // Calendar-time (non-business-days) branches use AddMinutes for the same precision.
        var remainingMinutes = Math.Max(0, durationHours.Value * 60 - cumulativeMinutes);

        DateTime dueAt;
        if (useBusinessDays)
        {
            dueAt = await businessTimeCalculator.AddBusinessHoursAsync(anchor.Value, remainingMinutes / 60, ct);
        }
        else
        {
            dueAt = anchor.Value.AddMinutes(remainingMinutes);
        }

        // (No workflow-umbrella cap: an activity's own SLA may extend past the end-to-end umbrella.)

        logger.LogDebug(
            "Calculated SLA for activity {ActivityId}: DueAt={DueAt}, AnchorType={AnchorType}, UseBusinessDays={UseBusinessDays}, Budget={Hours}h, Cumulative={CumulativeMin}min, Remaining={RemainingMin}min",
            activityId, dueAt, effectiveAnchorType, useBusinessDays, durationHours, cumulativeMinutes, remainingMinutes);

        // StartAt is the clock-start anchor (NOT necessarily AssignedAt) so the at-risk monitor can
        // measure the 75% threshold from where the budget actually began. DurationHours is the resolved
        // budget (persisted onto the task for display alongside the due date).
        return new SlaDeadline(dueAt, anchor.Value, durationHours);
    }

    public async Task<DateTime?> CalculateWorkflowDueAtAsync(
        Guid workflowDefinitionId,
        string? loanType,
        string? appraisalType,
        DateTime startedOn,
        CancellationToken ct = default)
    {
        // Query SlaPolicies with Scope = Workflow (Scope=3). WorkflowSlaConfigurations was dropped;
        // rows were backfilled by the AddSlaPolicyScopedUniqueIndexes migration before the table was removed.
        var policy = await dbContext.SlaPolicies
            .AsNoTracking()
            .Where(s => s.Scope == SlaPolicyScope.Workflow)
            .Where(s => s.WorkflowDefinitionId == workflowDefinitionId)
            .Where(s => s.LoanType == null || s.LoanType == loanType)
            .Where(s => s.AppraisalType == null || s.AppraisalType == appraisalType)
            .OrderBy(s => s.Priority)
            .ThenByDescending(s => s.LoanType != null)
            .ThenByDescending(s => s.AppraisalType != null)
            .FirstOrDefaultAsync(ct);

        if (policy is null)
        {
            logger.LogDebug("No workflow SLA policy for definition {DefinitionId}", workflowDefinitionId);
            return null;
        }

        DateTime dueAt;
        if (policy.UseBusinessDays)
        {
            dueAt = await businessTimeCalculator.AddBusinessHoursAsync(startedOn, policy.DurationHours, ct);
        }
        else
        {
            dueAt = startedOn.AddHours(policy.DurationHours);
        }

        logger.LogDebug(
            "Calculated workflow SLA: DueAt={DueAt}, TotalHours={Hours}", dueAt, policy.DurationHours);

        return dueAt;
    }

    public async Task<WorkflowSlaSnapshot?> GetWorkflowSlaSnapshotAsync(
        Guid workflowDefinitionId,
        string? loanType,
        string? appraisalType,
        DateTime startedOn,
        CancellationToken ct = default)
    {
        var policy = await dbContext.SlaPolicies
            .AsNoTracking()
            .Where(s => s.Scope == SlaPolicyScope.Workflow)
            .Where(s => s.WorkflowDefinitionId == workflowDefinitionId)
            .Where(s => s.LoanType == null || s.LoanType == loanType)
            .Where(s => s.AppraisalType == null || s.AppraisalType == appraisalType)
            .OrderBy(s => s.Priority)
            .ThenByDescending(s => s.LoanType != null)
            .ThenByDescending(s => s.AppraisalType != null)
            .FirstOrDefaultAsync(ct);

        if (policy is null) return null;

        DateTime dueAt;
        if (policy.UseBusinessDays)
        {
            dueAt = await businessTimeCalculator.AddBusinessHoursAsync(startedOn, policy.DurationHours, ct);
        }
        else
        {
            dueAt = startedOn.AddHours(policy.DurationHours);
        }

        return new WorkflowSlaSnapshot(policy.DurationHours, dueAt, policy.UseBusinessDays);
    }

    /// <remarks>
    /// <paramref name="workflowDefinitionId"/> is nullable here to allow global Stage policies
    /// (WorkflowDefinitionId = null) to match across any workflow definition. This is intentionally
    /// asymmetric with <see cref="CalculateActivityDueAtAsync"/>, which requires a non-null
    /// workflowDefinitionId because Activity-scope policies are always workflow-context-specific.
    /// </remarks>
    public async Task<DateTime?> CalculateStageDueAtAsync(
        Guid? workflowDefinitionId,
        string startActivityKey,
        DateTime startedAt,
        Guid? companyId,
        string? loanType,
        string? appraisalType,
        Guid? correlationId = null,
        DateTime? appointmentDate = null,
        CancellationToken ct = default)
    {
        var policy = await dbContext.SlaPolicies
            .AsNoTracking()
            .Where(p => p.Scope == SlaPolicyScope.Stage)
            .Where(p => p.StartActivityKey == startActivityKey)
            .Where(p => p.WorkflowDefinitionId == null || (workflowDefinitionId.HasValue && p.WorkflowDefinitionId == workflowDefinitionId))
            .Where(p => p.CompanyId == null || p.CompanyId == companyId)
            .Where(p => p.LoanType == null || p.LoanType == loanType)
            .Where(p => p.AppraisalType == null || p.AppraisalType == appraisalType)
            .OrderBy(p => p.Priority)
            .ThenByDescending(p => p.CompanyId != null)
            .ThenByDescending(p => p.LoanType != null)
            .ThenByDescending(p => p.AppraisalType != null)
            .FirstOrDefaultAsync(ct);

        if (policy is null) return null;

        return await ComputeStageDueAtAsync(policy, startedAt, appointmentDate, ct);
    }

    /// <summary>
    /// Computes a resolved Stage/window deadline as a FIXED close: anchor (Assignment ⇒
    /// <paramref name="startedAt"/> = the window's open; AppointmentDate ⇒ the appointment) + the full
    /// budget. This is the SHARED deadline for every member task and never refreshes on rework (the
    /// anchor is the window's earliest open). Consumed member time is deliberately NOT subtracted —
    /// subtracting it on top of anchoring at the window open would double-count and pull each later
    /// member's deadline earlier than the shared window close. (Per-activity rework deduction lives in
    /// <see cref="CalculateActivityDueAtAsync"/>, which anchors at the current assignment instead.)
    /// </summary>
    private async Task<DateTime?> ComputeStageDueAtAsync(
        SlaPolicy policy, DateTime startedAt, DateTime? appointmentDate, CancellationToken ct)
    {
        var effectiveAnchorType = policy.AnchorType ?? SlaAnchorType.Assignment;
        DateTime anchor;
        if (effectiveAnchorType == SlaAnchorType.AppointmentDate)
        {
            if (appointmentDate is null)
            {
                logger.LogDebug(
                    "Stage starting at {StartKey} is appointment-anchored but no appointment date is set — DueAt deferred",
                    policy.StartActivityKey);
                return null;
            }
            anchor = appointmentDate.Value;
        }
        else
        {
            anchor = startedAt;
        }

        return policy.UseBusinessDays
            ? await businessTimeCalculator.AddBusinessHoursAsync(anchor, policy.DurationHours, ct)
            : anchor.AddHours(policy.DurationHours);
    }

    /// <summary>
    /// If <paramref name="activityId"/> is a member of a Stage-scope window, returns that window's
    /// deadline so it GOVERNS the task (the per-activity clock is superseded). Returns null when the
    /// activity belongs to no window — the caller then keeps its own per-activity DueAt.
    /// The window is anchored on its START activity's entry time, so every member task shares one
    /// shrinking window deadline. The result also carries the window's AnchorType so callers can tell
    /// an appointment-anchored window (recompute on reschedule) from an assignment-anchored one.
    /// </summary>
    public async Task<GoverningStageResult?> ResolveGoverningStageDueAtAsync(
        string activityId,
        Guid workflowDefinitionId,
        Guid? companyId,
        string? loanType,
        string? appraisalType,
        DateTime assignedAt,
        Guid? correlationId = null,
        DateTime? appointmentDate = null,
        CancellationToken ct = default)
    {
        // Candidate windows for this workflow context, lowest Priority first (override 50 beats default
        // 100; specificity breaks ties) — same ordering as the other resolvers.
        var stagePolicies = await dbContext.SlaPolicies
            .AsNoTracking()
            .Where(p => p.Scope == SlaPolicyScope.Stage)
            .Where(p => p.WorkflowDefinitionId == null || p.WorkflowDefinitionId == workflowDefinitionId)
            .Where(p => p.CompanyId == null || p.CompanyId == companyId)
            .Where(p => p.LoanType == null || p.LoanType == loanType)
            .Where(p => p.AppraisalType == null || p.AppraisalType == appraisalType)
            .OrderBy(p => p.Priority)
            .ThenByDescending(p => p.CompanyId != null)
            .ThenByDescending(p => p.LoanType != null)
            .ThenByDescending(p => p.AppraisalType != null)
            .ToListAsync(ct);

        if (stagePolicies.Count == 0) return null;

        SlaPolicy? governing = null;
        foreach (var policy in stagePolicies)
        {
            var members = await BuildStageActivityIdsAsync(policy, ct);
            if (members.Contains(activityId))
            {
                governing = policy;
                break;
            }
        }

        if (governing is null) return null; // not a member of any window — keep the per-activity clock

        // Anchor the window on the START activity's entry time so every member task shares one deadline.
        var startEntry = await ResolveStageStartEntryAsync(governing.StartActivityKey!, correlationId, assignedAt, ct);
        var dueAt = await ComputeStageDueAtAsync(governing, startEntry, appointmentDate, ct);

        // The clock-start for the at-risk threshold: the appointment for appointment-anchored windows,
        // else the window's start-entry — shared by all members, never the late member's AssignedAt.
        var effectiveAnchorType = governing.AnchorType ?? SlaAnchorType.Assignment;
        DateTime? startAt = dueAt.HasValue
            ? (effectiveAnchorType == SlaAnchorType.AppointmentDate ? appointmentDate : startEntry)
            : null;

        return new GoverningStageResult(dueAt, effectiveAnchorType, startAt, governing.DurationHours);
    }

    /// <summary>
    /// Resolves when the workflow first entered a window's start activity: the earliest real
    /// (non-"Reassigned") CompletedTask leg for it; else the active PendingTask; else
    /// <paramref name="fallback"/> (the current activity IS the start, or no record yet ⇒ now).
    /// </summary>
    private async Task<DateTime> ResolveStageStartEntryAsync(
        string startActivityKey, Guid? correlationId, DateTime fallback, CancellationToken ct)
    {
        if (!correlationId.HasValue) return fallback;

        var firstCompleted = await dbContext.CompletedTasks
            .AsNoTracking()
            .Where(t => t.CorrelationId == correlationId.Value
                     && t.ActivityId == startActivityKey
                     && t.ActionTaken != "Reassigned")
            .OrderBy(t => t.AssignedAt)
            .Select(t => (DateTime?)t.AssignedAt)
            .FirstOrDefaultAsync(ct);

        if (firstCompleted.HasValue) return firstCompleted.Value;

        var pending = await dbContext.PendingTasks
            .AsNoTracking()
            .Where(t => t.CorrelationId == correlationId.Value && t.ActivityId == startActivityKey)
            .OrderBy(t => t.AssignedAt)
            .Select(t => (DateTime?)t.AssignedAt)
            .FirstOrDefaultAsync(ct);

        return pending ?? fallback;
    }

    /// <summary>
    /// Resolves the set of activity IDs covered by a stage policy.
    /// When <see cref="SlaPolicy.MiddleActivityKeys"/> is present it is authoritative
    /// (start ∪ middle ∪ end).  Otherwise, walks the workflow's forward transition graph
    /// from start to end via <see cref="WorkflowGraphHelper"/> — the same logic used by
    /// <c>SlaConfigEndpoints.GroupSpanActivities</c> so engine and admin screen always agree.
    /// </summary>
    private async Task<HashSet<string>> BuildStageActivityIdsAsync(SlaPolicy policy, CancellationToken ct)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(policy.StartActivityKey))
            return ids;

        // MiddleActivityKeys is the explicit override when present (handles non-contiguous spans).
        List<string>? parsedMiddle = null;
        if (!string.IsNullOrWhiteSpace(policy.MiddleActivityKeys))
        {
            try { parsedMiddle = JsonSerializer.Deserialize<List<string>>(policy.MiddleActivityKeys); }
            catch (JsonException) { /* fall through to graph walk */ }
        }

        if (parsedMiddle is not null)
        {
            ids.Add(policy.StartActivityKey);
            if (!string.IsNullOrWhiteSpace(policy.EndActivityKey))
                ids.Add(policy.EndActivityKey);
            foreach (var id in parsedMiddle.Where(id => !string.IsNullOrWhiteSpace(id)))
                ids.Add(id);
            return ids;
        }

        // No MiddleActivityKeys — walk the forward transition graph.
        if (!policy.WorkflowDefinitionId.HasValue || string.IsNullOrWhiteSpace(policy.EndActivityKey))
        {
            // Can't walk without a definition or an end key — return at least the start activity.
            ids.Add(policy.StartActivityKey);
            return ids;
        }

        var jsonDefinition = await dbContext.WorkflowDefinitions
            .AsNoTracking()
            .Where(d => d.Id == policy.WorkflowDefinitionId.Value)
            .Select(d => d.JsonDefinition)
            .FirstOrDefaultAsync(ct);

        if (jsonDefinition is null)
        {
            ids.Add(policy.StartActivityKey);
            return ids;
        }

        var transitions = WorkflowGraphHelper.GetOrParseTransitions(
            policy.WorkflowDefinitionId.Value, jsonDefinition);

        return WorkflowGraphHelper.GetForwardPathActivityIds(
            transitions, policy.StartActivityKey, policy.EndActivityKey);
    }
}
