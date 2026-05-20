namespace Workflow;

/// <summary>
/// Cross-module surface for forwarding admin input into an open workflow task.
/// Implemented in the Workflow module and consumed by other modules (e.g. Appraisal)
/// without exposing the Workflow DbContext or engine internals.
/// </summary>
public interface IWorkflowRelayService
{
    /// <summary>
    /// Completes an open human task on the given workflow instance by forwarding
    /// <paramref name="input"/> as the task's completion payload. The workflow engine
    /// advances the instance to the next activity (or terminal state) inside a single
    /// transaction. Integration events are published after the transaction commits.
    ///
    /// <paramref name="assigneeOverrides"/> lets the caller pin runtime assignees on
    /// activities that the workflow auto-routes to next (e.g. pinning an admin-selected
    /// internal appraiser onto <c>int-appraisal-execution</c> instead of letting the
    /// default round-robin strategy pick one).
    /// </summary>
    Task ResumeWorkflowAsync(
        Guid workflowInstanceId,
        string activityId,
        string completedBy,
        Dictionary<string, object> input,
        IReadOnlyDictionary<string, WorkflowAssigneeOverride>? assigneeOverrides = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Minimal cross-module DTO for pinning an assignee (or group, or strategy list) onto
/// a downstream activity. Translated to the workflow engine's internal RuntimeOverride
/// by the relay implementation.
/// </summary>
public sealed record WorkflowAssigneeOverride(
    string? Assignee = null,
    string? AssigneeGroup = null,
    IReadOnlyList<string>? AssignmentStrategies = null,
    string? Reason = null,
    string? OverrideBy = null);
