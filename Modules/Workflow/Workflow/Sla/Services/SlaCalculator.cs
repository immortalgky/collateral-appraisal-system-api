using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Workflow.Data;

namespace Workflow.Sla.Services;

public class SlaCalculator(
    WorkflowDbContext dbContext,
    IBusinessTimeCalculator businessTimeCalculator,
    ILogger<SlaCalculator> logger) : ISlaCalculator
{
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
        // 1. Look up SLA configuration (ordered by priority, lower wins)
        var config = await dbContext.SlaConfigurations
            .AsNoTracking()
            .Where(s => s.ActivityId == activityId || s.ActivityId == "*")
            .Where(s => s.WorkflowDefinitionId == null || s.WorkflowDefinitionId == workflowDefinitionId)
            .Where(s => s.CompanyId == null || s.CompanyId == companyId)
            .Where(s => s.LoanType == null || s.LoanType == loanType)
            .OrderBy(s => s.Priority)
            .FirstOrDefaultAsync(ct);

        int? durationHours = config?.DurationHours;
        bool useBusinessDays = config?.UseBusinessDays ?? false;

        // 2. Fall back to workflow JSON timeoutDuration
        if (durationHours is null && defaultTimeout.HasValue)
        {
            durationHours = (int)defaultTimeout.Value.TotalHours;
            useBusinessDays = false; // JSON defaults use calendar time
        }

        if (durationHours is null)
        {
            logger.LogDebug("No SLA config found for activity {ActivityId}, skipping", activityId);
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
        var config = await dbContext.WorkflowSlaConfigurations
            .AsNoTracking()
            .Where(s => s.WorkflowDefinitionId == workflowDefinitionId)
            .Where(s => s.LoanType == null || s.LoanType == loanType)
            .OrderBy(s => s.Priority)
            .FirstOrDefaultAsync(ct);

        if (config is null)
        {
            logger.LogDebug("No workflow SLA config for definition {DefinitionId}", workflowDefinitionId);
            return null;
        }

        DateTime dueAt;
        if (config.UseBusinessDays)
        {
            dueAt = await businessTimeCalculator.AddBusinessHoursAsync(startedOn, config.TotalDurationHours, ct);
        }
        else
        {
            dueAt = startedOn.AddHours(config.TotalDurationHours);
        }

        logger.LogDebug(
            "Calculated workflow SLA: DueAt={DueAt}, TotalHours={Hours}", dueAt, config.TotalDurationHours);

        return dueAt;
    }
}
