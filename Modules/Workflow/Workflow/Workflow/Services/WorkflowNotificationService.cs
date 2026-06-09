using Notification.Contracts.Realtime;

namespace Workflow.Workflow.Services;

public class WorkflowNotificationService(
    IRealtimeNotifier realtimeNotifier,
    IDateTimeProvider dateTimeProvider) : IWorkflowNotificationService
{
    public Task NotifyPoolTaskAssigned(string poolGroup, Guid taskId, string taskName)
    {
        var notification = new
        {
            Type = "PoolTaskAssigned",
            TaskId = taskId,
            TaskName = taskName,
            PoolGroup = poolGroup,
            Timestamp = dateTimeProvider.ApplicationNow
        };

        return realtimeNotifier.SendToGroupAsync($"pool-{poolGroup}", "PoolTaskUpdate", notification);
    }

    public Task NotifyPoolTaskStarted(string poolGroup, Guid taskId, string startedBy)
    {
        var notification = new
        {
            Type = "PoolTaskStarted",
            TaskId = taskId,
            StartedBy = startedBy,
            PoolGroup = poolGroup,
            Timestamp = dateTimeProvider.ApplicationNow
        };

        return realtimeNotifier.SendToGroupAsync($"pool-{poolGroup}", "PoolTaskUpdate", notification);
    }

    public Task NotifyPoolTaskClaimed(string poolGroup, Guid taskId, string claimedBy)
    {
        var notification = new
        {
            Type = "PoolTaskClaimed",
            TaskId = taskId,
            ClaimedBy = claimedBy,
            PoolGroup = poolGroup,
            Timestamp = dateTimeProvider.ApplicationNow
        };

        return realtimeNotifier.SendToGroupAsync($"pool-{poolGroup}", "PoolTaskUpdate", notification);
    }

    public Task NotifyPoolTaskLocked(string poolGroup, Guid taskId, string lockedBy)
    {
        var notification = new
        {
            Type = "PoolTaskLocked",
            TaskId = taskId,
            LockedBy = lockedBy,
            PoolGroup = poolGroup,
            Timestamp = dateTimeProvider.ApplicationNow
        };

        return realtimeNotifier.SendToGroupAsync($"pool-{poolGroup}", "PoolTaskUpdate", notification);
    }

    public Task NotifyPoolTaskUnlocked(string poolGroup, Guid taskId, string releasedBy)
    {
        var notification = new
        {
            Type = "PoolTaskUnlocked",
            TaskId = taskId,
            ReleasedBy = releasedBy,
            PoolGroup = poolGroup,
            Timestamp = dateTimeProvider.ApplicationNow
        };

        return realtimeNotifier.SendToGroupAsync($"pool-{poolGroup}", "PoolTaskUpdate", notification);
    }
}
