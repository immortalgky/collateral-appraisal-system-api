using Workflow.Workflow.Activities.Core;

namespace Workflow.Workflow.Services;

/// <summary>
/// Thin wrapper that exposes <see cref="IWorkflowService.ResumeWorkflowAsync"/> under the
/// cross-module <see cref="IWorkflowRelayService"/> contract so other modules (e.g. Appraisal)
/// can forward admin input into an open workflow task without a direct reference to the
/// Workflow DbContext or engine internals.
/// </summary>
public class WorkflowRelayService(IWorkflowService workflowService) : IWorkflowRelayService
{
    public Task ResumeWorkflowAsync(
        Guid workflowInstanceId,
        string activityId,
        string completedBy,
        Dictionary<string, object> input,
        IReadOnlyDictionary<string, WorkflowAssigneeOverride>? assigneeOverrides = null,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, RuntimeOverride>? runtimeOverrides = null;
        if (assigneeOverrides is { Count: > 0 })
        {
            runtimeOverrides = new Dictionary<string, RuntimeOverride>(assigneeOverrides.Count);
            foreach (var (key, value) in assigneeOverrides)
            {
                runtimeOverrides[key] = new RuntimeOverride
                {
                    RuntimeAssignee = value.Assignee,
                    RuntimeAssigneeGroup = value.AssigneeGroup,
                    RuntimeAssignmentStrategies = value.AssignmentStrategies?.ToList(),
                    OverrideReason = value.Reason,
                    OverrideBy = value.OverrideBy
                };
            }
        }

        return workflowService.ResumeWorkflowAsync(
            workflowInstanceId,
            activityId,
            completedBy,
            input,
            nextAssignmentOverrides: runtimeOverrides,
            cancellationToken);
    }
}
