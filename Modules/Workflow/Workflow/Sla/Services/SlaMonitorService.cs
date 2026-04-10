using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Workflow.Data;
using Workflow.Sla.Models;
using Workflow.Workflow.Models;
using MassTransit;
using Shared.Messaging.Events;

namespace Workflow.Sla.Services;

public class SlaMonitorService(
    IServiceScopeFactory scopeFactory,
    ILogger<SlaMonitorService> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SLA Monitor Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanForBreachesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during SLA breach scan");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }
    }

    private async Task ScanForBreachesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var now = DateTime.UtcNow;

        await ScanActivitySlaAsync(dbContext, connectionFactory, publishEndpoint, now, ct);
        await ScanWorkflowSlaAsync(dbContext, connectionFactory, now, ct);
    }

    private async Task ScanActivitySlaAsync(
        WorkflowDbContext dbContext, ISqlConnectionFactory connectionFactory,
        IPublishEndpoint publishEndpoint, DateTime now, CancellationToken ct)
    {
        // Only fetch IDs of tasks that actually need a status update
        using var conn = connectionFactory.GetOpenConnection();
        var breachedIds = (await conn.QueryAsync<Guid>(
            """
            SELECT Id FROM workflow.PendingTasks
            WHERE DueAt IS NOT NULL AND DueAt <= @Now AND SlaStatus != 'BREACHED'
            """, new { Now = now })).ToList();

        var atRiskIds = (await conn.QueryAsync<Guid>(
            """
            SELECT Id FROM workflow.PendingTasks
            WHERE DueAt IS NOT NULL AND SlaStatus = 'ON_TIME'
              AND DATEADD(SECOND, DATEDIFF(SECOND, AssignedAt, DueAt) * 0.75, AssignedAt) <= @Now
            """, new { Now = now })).ToList();

        if (breachedIds.Count == 0 && atRiskIds.Count == 0) return;

        var breachLogs = new List<SlaBreachLog>();

        // Process breached tasks
        if (breachedIds.Count > 0)
        {
            var tasks = await dbContext.PendingTasks
                .Where(t => breachedIds.Contains(t.Id))
                .ToListAsync(ct);

            foreach (var task in tasks)
            {
                task.MarkBreached(now);
                breachLogs.Add(SlaBreachLog.Create(
                    task.Id, task.CorrelationId, task.TaskName,
                    task.AssignedTo, task.DueAt!.Value, now, "BREACHED"));

                await publishEndpoint.Publish(new SlaBreachIntegrationEvent
                {
                    CorrelationId = task.CorrelationId,
                    PendingTaskId = task.Id,
                    TaskName = task.TaskName,
                    AssignedTo = task.AssignedTo,
                    SlaStatus = "BREACHED",
                    DueAt = task.DueAt.Value,
                    DetectedAt = now
                }, ct);

                logger.LogWarning(
                    "SLA BREACHED: Task {TaskId} for {TaskName}, assigned to {AssignedTo}, was due at {DueAt}",
                    task.Id, task.TaskName, task.AssignedTo, task.DueAt);
            }
        }

        // Process at-risk tasks
        if (atRiskIds.Count > 0)
        {
            var tasks = await dbContext.PendingTasks
                .Where(t => atRiskIds.Contains(t.Id))
                .ToListAsync(ct);

            foreach (var task in tasks)
            {
                task.MarkAtRisk();
                breachLogs.Add(SlaBreachLog.Create(
                    task.Id, task.CorrelationId, task.TaskName,
                    task.AssignedTo, task.DueAt!.Value, now, "AT_RISK"));

                await publishEndpoint.Publish(new SlaBreachIntegrationEvent
                {
                    CorrelationId = task.CorrelationId,
                    PendingTaskId = task.Id,
                    TaskName = task.TaskName,
                    AssignedTo = task.AssignedTo,
                    SlaStatus = "AT_RISK",
                    DueAt = task.DueAt.Value,
                    DetectedAt = now
                }, ct);

                logger.LogWarning(
                    "SLA AT RISK: Task {TaskId} for {TaskName}, assigned to {AssignedTo}, due at {DueAt}",
                    task.Id, task.TaskName, task.AssignedTo, task.DueAt);
            }
        }

        if (breachLogs.Count > 0)
        {
            dbContext.SlaBreachLogs.AddRange(breachLogs);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Processed {Count} SLA status changes", breachLogs.Count);
        }
    }

    private async Task ScanWorkflowSlaAsync(
        WorkflowDbContext dbContext, ISqlConnectionFactory connectionFactory,
        DateTime now, CancellationToken ct)
    {
        using var conn = connectionFactory.GetOpenConnection();
        var breachedIds = (await conn.QueryAsync<Guid>(
            """
            SELECT Id FROM workflow.WorkflowInstances
            WHERE Status = 'RUNNING' AND WorkflowDueAt IS NOT NULL
              AND WorkflowDueAt <= @Now AND WorkflowSlaStatus != 'BREACHED'
            """, new { Now = now })).ToList();

        var atRiskIds = (await conn.QueryAsync<Guid>(
            """
            SELECT Id FROM workflow.WorkflowInstances
            WHERE Status = 'RUNNING' AND WorkflowDueAt IS NOT NULL
              AND WorkflowSlaStatus = 'ON_TIME'
              AND DATEADD(SECOND, DATEDIFF(SECOND, StartedOn, WorkflowDueAt) * 0.75, StartedOn) <= @Now
            """, new { Now = now })).ToList();

        if (breachedIds.Count == 0 && atRiskIds.Count == 0) return;

        var changed = false;

        if (breachedIds.Count > 0)
        {
            var workflows = await dbContext.WorkflowInstances
                .Where(w => breachedIds.Contains(w.Id))
                .ToListAsync(ct);
            foreach (var wf in workflows)
            {
                wf.MarkWorkflowBreached();
                changed = true;
                logger.LogWarning("WORKFLOW SLA BREACHED: Instance {InstanceId}, due at {DueAt}",
                    wf.Id, wf.WorkflowDueAt);
            }
        }

        if (atRiskIds.Count > 0)
        {
            var workflows = await dbContext.WorkflowInstances
                .Where(w => atRiskIds.Contains(w.Id))
                .ToListAsync(ct);
            foreach (var wf in workflows)
            {
                wf.MarkWorkflowAtRisk();
                changed = true;
                logger.LogWarning("WORKFLOW SLA AT RISK: Instance {InstanceId}, due at {DueAt}",
                    wf.Id, wf.WorkflowDueAt);
            }
        }

        if (changed)
            await dbContext.SaveChangesAsync(ct);
    }
}
