using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Workflow.Data;
using Workflow.Sla.Models;

namespace Workflow.Sla.Services;

public class SlaCalculator(
    WorkflowDbContext dbContext,
    Shared.Sla.IBusinessTimeCalculator businessTimeCalculator,
    ILogger<SlaCalculator> logger) : ISlaCalculator
{
    /// <remarks>
    /// <paramref name="workflowDefinitionId"/> is non-nullable here because Activity-scope policies
    /// must always be resolved within a known workflow context — callers should never need to search
    /// across all workflow definitions for an activity. This is intentionally asymmetric with
    /// <see cref="CalculateStageDueAtAsync"/>, which accepts a nullable workflowDefinitionId to
    /// support global Stage policies that apply regardless of workflow definition.
    /// </remarks>
    public async Task<DateTime?> CalculateActivityDueAtAsync(
        string activityId,
        Guid workflowDefinitionId,
        Guid? companyId,
        string? loanType,
        DateTime assignedAt,
        TimeSpan? defaultTimeout,
        DateTime? workflowDueAt,
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
            .OrderBy(s => s.Priority)
            .FirstOrDefaultAsync(ct);

        int? durationHours = policy?.DurationHours;
        bool useBusinessDays = policy?.UseBusinessDays ?? false;

        // 2. Fall back to workflow JSON timeoutDuration
        if (durationHours is null && defaultTimeout.HasValue)
        {
            durationHours = (int)defaultTimeout.Value.TotalHours;
            useBusinessDays = false; // JSON defaults use calendar time
        }

        if (durationHours is null)
        {
            logger.LogDebug("No SLA policy found for activity {ActivityId}, skipping", activityId);
            return null;
        }

        // 3. Calculate DueAt
        DateTime dueAt;
        if (useBusinessDays)
        {
            dueAt = await businessTimeCalculator.AddBusinessHoursAsync(assignedAt, durationHours.Value, ct);
        }
        else
        {
            dueAt = assignedAt.AddHours(durationHours.Value);
        }

        // 4. Cap by workflow deadline
        if (workflowDueAt.HasValue && dueAt > workflowDueAt.Value)
        {
            dueAt = workflowDueAt.Value;
        }

        logger.LogDebug(
            "Calculated SLA for activity {ActivityId}: DueAt={DueAt}, UseBusinessDays={UseBusinessDays}, DurationHours={Hours}",
            activityId, dueAt, useBusinessDays, durationHours);

        return dueAt;
    }

    public async Task<DateTime?> CalculateWorkflowDueAtAsync(
        Guid workflowDefinitionId,
        string? loanType,
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
            .OrderBy(s => s.Priority)
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
        DateTime startedOn,
        CancellationToken ct = default)
    {
        var policy = await dbContext.SlaPolicies
            .AsNoTracking()
            .Where(s => s.Scope == SlaPolicyScope.Workflow)
            .Where(s => s.WorkflowDefinitionId == workflowDefinitionId)
            .Where(s => s.LoanType == null || s.LoanType == loanType)
            .OrderBy(s => s.Priority)
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
        CancellationToken ct = default)
    {
        var policy = await dbContext.SlaPolicies
            .AsNoTracking()
            .Where(p => p.Scope == SlaPolicyScope.Stage)
            .Where(p => p.StartActivityKey == startActivityKey)
            .Where(p => p.WorkflowDefinitionId == null || (workflowDefinitionId.HasValue && p.WorkflowDefinitionId == workflowDefinitionId))
            .Where(p => p.CompanyId == null || p.CompanyId == companyId)
            .Where(p => p.LoanType == null || p.LoanType == loanType)
            .OrderBy(p => p.Priority)
            .FirstOrDefaultAsync(ct);

        if (policy is null) return null;

        return policy.UseBusinessDays
            ? await businessTimeCalculator.AddBusinessHoursAsync(startedAt, policy.DurationHours, ct)
            : startedAt.AddHours(policy.DurationHours);
    }
}
