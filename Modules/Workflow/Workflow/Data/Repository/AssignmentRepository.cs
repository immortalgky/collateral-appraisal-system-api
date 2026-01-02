using Shared.Messaging.Values;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using Dapper;
using Shared.Data;

namespace Workflow.Data.Repository;

public class AssignmentRepository(WorkflowDbContext dbContext, ISqlConnectionFactory sqlConnectionFactory)
    : IAssignmentRepository
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory = sqlConnectionFactory;

    public Task<List<PendingTask>> GetPendingTaskAsync(string userCode, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<PendingTask?> GetPendingTaskAsync(Guid correlationId, TaskName taskName,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PendingTasks
            .FirstOrDefaultAsync(
                x => x.CorrelationId == correlationId && x.TaskName == taskName,
                cancellationToken
            );
    }

    public async Task AddTaskAsync(PendingTask pendingTask, CancellationToken cancellationToken = default)
    {
        dbContext.PendingTasks.Add(pendingTask);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddCompletedTaskAsync(CompletedTask completedTask, CancellationToken cancellationToken = default)
    {
        dbContext.CompletedTasks.Add(completedTask);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemovePendingTaskAsync(PendingTask pendingTask,
        CancellationToken cancellationToken = default)
    {
        dbContext.PendingTasks.Remove(pendingTask);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CompletedTask?> GetLastCompletedTaskForIdAsync(Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CompletedTasks
            .Where(x => x.CorrelationId == correlationId)
            .OrderByDescending(x => x.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CompletedTask?> GetLastCompletedTaskForActivityAsync(TaskName activityName,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CompletedTasks
            .Where(x => x.TaskName == activityName)
            .OrderByDescending(x => x.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CompletedTask?> GetLastCompletedTaskForIdAndActivityAsync(Guid correlationId,
        TaskName activityName,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CompletedTasks
            .Where(x => x.CorrelationId == correlationId && x.TaskName == activityName)
            .OrderByDescending(x => x.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> GetActiveTaskCountForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.PendingTasks
            .CountAsync(x => x.AssignedTo == userId, cancellationToken);
    }


    public async Task SyncUsersForGroupCombinationAsync(string activityName, string groupsHash, string groupsList,
        List<string> eligibleUsers, CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Ambient transaction required for SyncUsersForGroupCombinationAsync");

        try
        {
            // Mark all existing users for this combination as inactive
            var existingUsers = await dbContext.RoundRobinQueue
                .Where(x => x.ActivityName == activityName && x.GroupsHash == groupsHash)
                .ToListAsync(cancellationToken);

            foreach (var existingUser in existingUsers) existingUser.IsActive = false;

            // Add or reactivate current eligible users
            foreach (var userId in eligibleUsers)
            {
                var existingCounter = existingUsers.FirstOrDefault(x => x.UserId == userId);

                // TODO: Implement logic to handle case where user is on leave or inactive
                if (existingCounter != null)
                    // Reactivate an existing user
                    existingCounter.IsActive = true;
                else
                    // Add new user
                    dbContext.RoundRobinQueue.Add(new RoundRobinQueue
                    {
                        ActivityName = activityName,
                        GroupsHash = groupsHash,
                        GroupsList = groupsList,
                        UserId = userId,
                        AssignmentCount = 0,
                        LastAssignedAt = DateTime.Now,
                        IsActive = true
                    });
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            throw;
        }
    }

    public async Task<string?> SelectNextUserWithRoundResetAsync(string activityName, string groupsHash,
        CancellationToken cancellationToken = default)
    {
        // Lock all rows for this activity and group combination
        var counters = await dbContext.RoundRobinQueue
            .Where(x => x.ActivityName == activityName && x.GroupsHash == groupsHash && x.IsActive)
            .OrderBy(x => x.AssignmentCount)
            .ThenBy(x => x.UserId)
            .ToListAsync(cancellationToken);

        if (!counters.Any()) return null;

        // Select user with minimum count
        var selectedUser = counters.First();
        selectedUser.AssignmentCount++;
        selectedUser.LastAssignedAt = DateTime.Now;

        // Check if the round is complete (no active users have count = 0)
        var usersWithZero = counters.Count(x => x.AssignmentCount == 0);

        if (usersWithZero == 0) // No users left with zero counts
            // Reset all counters for this combination
            foreach (var counter in counters)
                counter.AssignmentCount = 0;

        await dbContext.SaveChangesAsync(cancellationToken);

        return selectedUser.UserId;
    }
}