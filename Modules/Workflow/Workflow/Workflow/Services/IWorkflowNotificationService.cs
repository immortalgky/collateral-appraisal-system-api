namespace Workflow.Workflow.Services;

public interface IWorkflowNotificationService
{
    // Pool task notifications
    Task NotifyPoolTaskAssigned(string poolGroup, Guid taskId, string taskName);
    Task NotifyPoolTaskStarted(string poolGroup, Guid taskId, string startedBy);
    Task NotifyPoolTaskClaimed(string poolGroup, Guid taskId, string claimedBy);
    Task NotifyPoolTaskLocked(string poolGroup, Guid taskId, string lockedBy);
    Task NotifyPoolTaskUnlocked(string poolGroup, Guid taskId, string releasedBy);
}
