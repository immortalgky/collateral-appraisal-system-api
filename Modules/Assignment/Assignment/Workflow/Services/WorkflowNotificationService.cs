using Assignment.Workflow.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Assignment.Workflow.Services;

public class WorkflowNotificationService : IWorkflowNotificationService
{
    private readonly IHubContext<WorkflowHub> _hubContext;

    public WorkflowNotificationService(IHubContext<WorkflowHub> hubContext)
    {
        _hubContext = hubContext;
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

    public async Task NotifyActivityCompleted(Guid workflowInstanceId, string activityId, string completedBy, Dictionary<string, object> outputData)
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

    public async Task NotifyActivityAssigned(Guid workflowInstanceId, string activityId, string assignedTo, string activityName)
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
}