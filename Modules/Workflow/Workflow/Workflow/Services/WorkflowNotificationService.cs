using Workflow.Workflow.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Workflow.Workflow.Services;

public class WorkflowNotificationService : IWorkflowNotificationService
{
    private readonly IHubContext<WorkflowHub> _hubContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public WorkflowNotificationService(IHubContext<WorkflowHub> hubContext, IDateTimeProvider dateTimeProvider)
    {
        _hubContext = hubContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task NotifyWorkflowStarted(Guid workflowInstanceId, string instanceName, string startedBy)
    {
        var notification = new
        {
            Type = "WorkflowStarted",
            WorkflowInstanceId = workflowInstanceId,
            InstanceName = instanceName,
            StartedBy = startedBy,
            Timestamp = DateTime.UtcNow
        };

        await _hubContext.Clients.Group($"workflow-{workflowInstanceId}")
            .SendAsync("WorkflowUpdate", notification);
    }

    public async Task NotifyActivityCompleted(Guid workflowInstanceId, string activityId, string completedBy,
        Dictionary<string, object> outputData)
    {
        var notification = new
        {
            Type = "ActivityCompleted",
            WorkflowInstanceId = workflowInstanceId,
            ActivityId = activityId,
            CompletedBy = completedBy,
            OutputData = outputData,
            Timestamp = DateTime.UtcNow
        };

        await _hubContext.Clients.Group($"workflow-{workflowInstanceId}")
            .SendAsync("WorkflowUpdate", notification);
    }

    public async Task NotifyActivityAssigned(Guid workflowInstanceId, string activityId, string assignedTo,
        string activityName)
    {
        var notification = new
        {
            Type = "ActivityAssigned",
            WorkflowInstanceId = workflowInstanceId,
            ActivityId = activityId,
            AssignedTo = assignedTo,
            ActivityName = activityName,
            Timestamp = DateTime.UtcNow
        };

        await _hubContext.Clients.Group($"workflow-{workflowInstanceId}")
            .SendAsync("WorkflowUpdate", notification);

        // Also notify the specific user
        await _hubContext.Clients.Group($"user-{assignedTo}")
            .SendAsync("TaskAssigned", notification);
    }

    public async Task NotifyWorkflowCompleted(Guid workflowInstanceId, string completedBy)
    {
        var notification = new
        {
            Type = "WorkflowCompleted",
            WorkflowInstanceId = workflowInstanceId,
            CompletedBy = completedBy,
            Timestamp = DateTime.UtcNow
        };

        await _hubContext.Clients.Group($"workflow-{workflowInstanceId}")
            .SendAsync("WorkflowUpdate", notification);
    }

    public async Task NotifyWorkflowFailed(Guid workflowInstanceId, string errorMessage)
    {
        var notification = new
        {
            Type = "WorkflowFailed",
            WorkflowInstanceId = workflowInstanceId,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.UtcNow
        };

        await _hubContext.Clients.Group($"workflow-{workflowInstanceId}")
            .SendAsync("WorkflowUpdate", notification);
    }

    public async Task NotifyWorkflowCancelled(Guid workflowInstanceId, string cancelledBy, string reason)
    {
        var notification = new
        {
            Type = "WorkflowCancelled",
            WorkflowInstanceId = workflowInstanceId,
            CancelledBy = cancelledBy,
            Reason = reason,
            Timestamp = DateTime.UtcNow
        };

        await _hubContext.Clients.Group($"workflow-{workflowInstanceId}")
            .SendAsync("WorkflowUpdate", notification);
    }

    public async Task NotifyUserTaskAssigned(string userId, Guid workflowInstanceId, string taskName, string activityId)
    {
        var notification = new
        {
            Type = "UserTaskAssigned",
            WorkflowInstanceId = workflowInstanceId,
            TaskName = taskName,
            ActivityId = activityId,
            Timestamp = DateTime.UtcNow
        };

        await _hubContext.Clients.Group($"user-{userId}")
            .SendAsync("TaskAssigned", notification);
    }

    public async Task NotifyPoolTaskAssigned(string poolGroup, Guid taskId, string taskName)
    {
        var notification = new
        {
            Type = "PoolTaskAssigned",
            TaskId = taskId,
            TaskName = taskName,
            PoolGroup = poolGroup,
            Timestamp = _dateTimeProvider.ApplicationNow
        };

        await _hubContext.Clients.Group($"pool-{poolGroup}")
            .SendAsync("PoolTaskUpdate", notification);
    }

    public async Task NotifyPoolTaskStarted(string poolGroup, Guid taskId, string startedBy)
    {
        var notification = new
        {
            Type = "PoolTaskStarted",
            TaskId = taskId,
            StartedBy = startedBy,
            PoolGroup = poolGroup,
            Timestamp = _dateTimeProvider.ApplicationNow
        };

        await _hubContext.Clients.Group($"pool-{poolGroup}")
            .SendAsync("PoolTaskUpdate", notification);
    }

    public async Task NotifyPoolTaskClaimed(string poolGroup, Guid taskId, string claimedBy)
    {
        var notification = new
        {
            Type = "PoolTaskClaimed",
            TaskId = taskId,
            ClaimedBy = claimedBy,
            PoolGroup = poolGroup,
            Timestamp = _dateTimeProvider.ApplicationNow
        };

        await _hubContext.Clients.Group($"pool-{poolGroup}")
            .SendAsync("PoolTaskUpdate", notification);
    }

    public async Task NotifyPoolTaskLocked(string poolGroup, Guid taskId, string lockedBy)
    {
        var notification = new
        {
            Type = "PoolTaskLocked",
            TaskId = taskId,
            LockedBy = lockedBy,
            PoolGroup = poolGroup,
            Timestamp = _dateTimeProvider.ApplicationNow
        };

        await _hubContext.Clients.Group($"pool-{poolGroup}")
            .SendAsync("PoolTaskUpdate", notification);
    }

    public async Task NotifyPoolTaskUnlocked(string poolGroup, Guid taskId, string releasedBy)
    {
        var notification = new
        {
            Type = "PoolTaskUnlocked",
            TaskId = taskId,
            ReleasedBy = releasedBy,
            PoolGroup = poolGroup,
            Timestamp = _dateTimeProvider.ApplicationNow
        };

        await _hubContext.Clients.Group($"pool-{poolGroup}")
            .SendAsync("PoolTaskUpdate", notification);
    }
}