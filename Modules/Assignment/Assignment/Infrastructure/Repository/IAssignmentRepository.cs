using Shared.Messaging.Values;

namespace Assignment.Data.Repository;

public interface IAssignmentRepository
{
    Task<List<PendingTask>> GetPendingTaskAsync(string userCode, CancellationToken cancellationToken = default);

    Task<PendingTask?> GetPendingTaskAsync(Guid correlationId, TaskName taskName,
        CancellationToken cancellationToken = default);

    Task AddTaskAsync(PendingTask pendingTask, CancellationToken cancellationToken = default);
    Task AddCompletedTaskAsync(CompletedTask completedTask, CancellationToken cancellationToken = default);
    Task RemovePendingTaskAsync(PendingTask pendingTask, CancellationToken cancellationToken = default);

    Task<CompletedTask?> GetLastCompletedTaskForIdAsync(Guid correlationId,
        CancellationToken cancellationToken = default);

    Task<CompletedTask?> GetLastCompletedTaskForActivityAsync(TaskName activityName,
        CancellationToken cancellationToken = default);

    Task<CompletedTask?> GetLastCompletedTaskForIdAndActivityAsync(Guid correlationId, TaskName activityName,
        CancellationToken cancellationToken = default);

    Task<int> GetActiveTaskCountForUserAsync(string userId, CancellationToken cancellationToken = default);

    Task SyncUsersForGroupCombinationAsync(string activityName, string groupsHash, string groupsList,
        List<string> eligibleUsers, CancellationToken cancellationToken = default);

    Task<string?> SelectNextUserWithRoundResetAsync(string activityName, string groupsHash,
        CancellationToken cancellationToken = default);
}