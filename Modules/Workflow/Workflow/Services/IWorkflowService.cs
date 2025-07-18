namespace Workflow.Services;

public interface IWorkflowService
{
    System.Threading.Tasks.Task StartWorkflowAsync(Guid requestId);
    System.Threading.Tasks.Task DecisionAsync(string correlationId, string activityName, string actiontaken);
}