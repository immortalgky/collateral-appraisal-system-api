namespace Workflow.Data.Repository;

public interface IAssignmentRepository
{
    Task<List<PendingTask>> GetPendingTaskAsync(string userCode, CancellationToken cancellationToken = default);

    Task<PendingTask?> GetPendingTaskAsync(Guid correlationId, string taskName,
        CancellationToken cancellationToken = default);

    Task<PendingTask?> GetPendingTaskByCorrelationIdAsync(Guid correlationId,
        CancellationToken cancellationToken = default);

    Task<PendingTask?> GetPendingTaskByWorkflowInstanceIdAsync(Guid workflowInstanceId,
        CancellationToken cancellationToken = default);

    Task AddTaskAsync(PendingTask pendingTask, CancellationToken cancellationToken = default);
    Task AddCompletedTaskAsync(CompletedTask completedTask, CancellationToken cancellationToken = default);
    Task RemovePendingTaskAsync(PendingTask pendingTask, CancellationToken cancellationToken = default);

    Task<CompletedTask?> GetLastCompletedTaskForIdAsync(Guid correlationId,
        CancellationToken cancellationToken = default);

    Task<CompletedTask?> GetLastCompletedTaskForActivityAsync(string activityName,
        CancellationToken cancellationToken = default);

    Task<CompletedTask?> GetLastCompletedTaskForIdAndActivityAsync(Guid correlationId, string activityName,
        CancellationToken cancellationToken = default);

    Task<int> GetActiveTaskCountForUserAsync(string userId, CancellationToken cancellationToken = default);

    Task SyncUsersForGroupCombinationAsync(string activityName, string groupsHash, string groupsList,
        List<string> eligibleUsers, CancellationToken cancellationToken = default);

    Task<string?> SelectNextUserWithRoundResetAsync(string activityName, string groupsHash,
        CancellationToken cancellationToken = default);

    Task<List<PendingTask>> GetPendingTasksByCorrelationIdAsync(Guid correlationId,
        CancellationToken cancellationToken = default);

    Task<List<PendingTask>> GetPendingTasksByWorkflowInstanceIdAsync(Guid workflowInstanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns pending fan-out tasks for a specific workflow instance + activity pair.
    /// Used by FanOutTaskActivity to check all-terminal condition.
    /// </summary>
    Task<List<PendingTask>> GetFanOutPendingTasksAsync(Guid workflowInstanceId, string activityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the fan-out task for a specific company within a workflow instance + activity pair.
    /// </summary>
    Task<PendingTask?> GetFanOutTaskByCompanyAsync(Guid workflowInstanceId, string activityId, Guid companyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the fan-out task for a specific company identified by correlation id + activity pair.
    /// Used when only the CorrelationId (e.g. QuotationRequestId) is known, not the WorkflowInstanceId.
    /// </summary>
    Task<PendingTask?> GetFanOutTaskByCorrelationIdAndCompanyAsync(
        Guid correlationId,
        string activityId,
        Guid assigneeCompanyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns completed fan-out tasks for a given correlation id where AssigneeCompanyId matches.
    /// Used to determine whether an external company caller has historical ownership on a quotation workflow.
    /// </summary>
    Task<List<CompletedTask>> GetCompletedFanOutTasksByCorrelationIdAndCompanyAsync(
        Guid correlationId,
        Guid assigneeCompanyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns completed tasks for a given correlation id that the specified user directly owns
    /// (matched via PoolTaskAccess candidate set: username, groups, team).
    /// Used for RM / non-fan-out historical ownership checks where no company filter applies.
    /// </summary>
    Task<List<CompletedTask>> GetCompletedTasksByCorrelationIdForUserAsync(
        Guid correlationId,
        string username,
        IReadOnlyList<string> userGroups,
        string? userTeamId,
        Guid? callerCompanyId,
        CancellationToken cancellationToken = default);
}
