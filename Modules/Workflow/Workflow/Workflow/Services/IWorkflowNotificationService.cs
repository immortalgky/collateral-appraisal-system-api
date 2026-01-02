namespace Workflow.Workflow.Services;

public interface IWorkflowNotificationService
{
    Task NotifyWorkflowStarted(Guid workflowInstanceId, string instanceName, string startedBy);
    Task NotifyActivityCompleted(Guid workflowInstanceId, string activityId, string completedBy, Dictionary<string, object> outputData);
    Task NotifyActivityAssigned(Guid workflowInstanceId, string activityId, string assignedTo, string activityName);
    Task NotifyWorkflowCompleted(Guid workflowInstanceId, string completedBy);
    Task NotifyWorkflowFailed(Guid workflowInstanceId, string errorMessage);
    Task NotifyWorkflowCancelled(Guid workflowInstanceId, string cancelledBy, string reason);
    Task NotifyUserTaskAssigned(string userId, Guid workflowInstanceId, string taskName, string activityId);
}