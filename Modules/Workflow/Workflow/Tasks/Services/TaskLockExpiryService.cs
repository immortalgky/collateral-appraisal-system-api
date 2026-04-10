using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Workflow.Data;
using Workflow.Workflow.Services;

namespace Workflow.Tasks.Services;

public class TaskLockExpiryService(IServiceScopeFactory scopeFactory, ILogger<TaskLockExpiryService> logger)
    : BackgroundService
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);
            await ReleaseExpiredLocksAsync(stoppingToken);
        }
    }

    private async Task ReleaseExpiredLocksAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<IWorkflowNotificationService>();

        var cutoff = DateTime.UtcNow.Subtract(LockTimeout);
        var expiredTasks = await dbContext.PendingTasks
            .Where(t => t.AssignedType == "2" && t.WorkingBy != null && t.LockedAt != null && t.LockedAt < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var task in expiredTasks)
        {
            var poolGroup = task.AssignedTo;
            task.ReleaseLock();
            await notificationService.NotifyPoolTaskUnlocked(poolGroup, task.Id, "timeout");
        }

        if (expiredTasks.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Released {Count} expired task locks", expiredTasks.Count);
        }
    }
}
