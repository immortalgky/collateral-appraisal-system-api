using Assignment.Workflow.Models;
using Assignment.Workflow.Schema;

namespace Assignment.Workflow.Engine;

public interface IWorkflowEngine
{
    Task<WorkflowInstance> StartWorkflowAsync(
        Guid workflowDefinitionId,
        string instanceName,
        string startedBy,
        Dictionary<string, object>? initialVariables = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    Task<WorkflowInstance> ResumeWorkflowAsync(
        Guid workflowInstanceId,
        string activityId,
        Dictionary<string, object> outputData,
        string completedBy,
        string? comments = null,
        CancellationToken cancellationToken = default);

    Task<WorkflowInstance?> GetWorkflowInstanceAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<WorkflowInstance>> GetUserTasksAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateWorkflowDefinitionAsync(
        WorkflowSchema workflowSchema,
        CancellationToken cancellationToken = default);

    Task CancelWorkflowAsync(
        Guid workflowInstanceId,
        string cancelledBy,
        string reason,
        CancellationToken cancellationToken = default);
}