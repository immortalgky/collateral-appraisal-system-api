namespace Assignment.Data.Repository;

public interface IAssignmentRepository
{
<<<<<<< HEAD
    Task<Assignments.Models.Assignment> GetAssignmentById(long requestId, bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    Task<Assignments.Models.Assignment> CreateAssignment(Assignments.Models.Assignment assignment,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAssignment(long Id,Assignments.Models.Assignment assignment,
        CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
=======
    Task<List<PendingTask>> GetPendingTaskAsync(string userCode, CancellationToken cancellationToken = default);

    Task<PendingTask?> GetPendingTaskAsync(Guid correlationId, string taskName,
        CancellationToken cancellationToken = default);

    Task AddTaskAsync(PendingTask pendingTask, CancellationToken cancellationToken = default);
    Task AddCompletedTaskAsync(CompletedTask completedTask, CancellationToken cancellationToken = default);
    Task RemovePendingTaskAsync(PendingTask pendingTask, CancellationToken cancellationToken = default);

    Task<CompletedTask?> GetLastCompletedTaskForActivityAsync(string activityName,
        CancellationToken cancellationToken = default);

    Task<int> GetActiveTaskCountForUserAsync(string userId, CancellationToken cancellationToken = default);

    Task SyncUsersForGroupCombinationAsync(string activityName, string groupsHash, string groupsList,
        List<string> eligibleUsers, CancellationToken cancellationToken = default);

    Task<string?> SelectNextUserWithRoundResetAsync(string activityName, string groupsHash,
        CancellationToken cancellationToken = default);
>>>>>>> 3434ca92f42e614d268511d3a6e0d95cb6f4d666
}