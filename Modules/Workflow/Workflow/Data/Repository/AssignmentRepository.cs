using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using Dapper;
using Shared.Data;
using Workflow.Tasks.Authorization;

namespace Workflow.Data.Repository;

public class AssignmentRepository(WorkflowDbContext dbContext, ISqlConnectionFactory sqlConnectionFactory)
    : IAssignmentRepository
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory = sqlConnectionFactory;

    public Task<List<PendingTask>> GetPendingTaskAsync(string userCode, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<PendingTask?> GetPendingTaskAsync(Guid correlationId, string taskName,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PendingTasks
            .FirstOrDefaultAsync(
                x => x.CorrelationId == correlationId && x.TaskName == taskName,
                cancellationToken
            );
    }

    public async Task<PendingTask?> GetPendingTaskByCorrelationIdAsync(Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PendingTasks
            .FirstOrDefaultAsync(
                x => x.CorrelationId == correlationId,
                cancellationToken
            );
    }

    public async Task<PendingTask?> GetPendingTaskByWorkflowInstanceIdAsync(Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PendingTasks
            .FirstOrDefaultAsync(
                x => x.WorkflowInstanceId == workflowInstanceId,
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

    public async Task<CompletedTask?> GetLastCompletedTaskForActivityAsync(string activityName,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CompletedTasks
            .Where(x => x.TaskName == activityName)
            .OrderByDescending(x => x.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CompletedTask?> GetLastCompletedTaskForIdAndActivityAsync(Guid correlationId,
        string activityName,
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

    public async Task<List<PendingTask>> GetPendingTasksByCorrelationIdAsync(Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PendingTasks
            .Where(x => x.CorrelationId == correlationId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PendingTask>> GetPendingTasksByWorkflowInstanceIdAsync(Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PendingTasks
            .Where(x => x.WorkflowInstanceId == workflowInstanceId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PendingTask>> GetFanOutPendingTasksAsync(
        Guid workflowInstanceId, string activityId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PendingTasks
            .Where(x => x.WorkflowInstanceId == workflowInstanceId && x.ActivityId == activityId)
            .ToListAsync(cancellationToken);
    }

    public async Task<PendingTask?> GetFanOutTaskByCompanyAsync(
        Guid workflowInstanceId, string activityId, Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PendingTasks
            .FirstOrDefaultAsync(
                x => x.WorkflowInstanceId == workflowInstanceId
                     && x.ActivityId == activityId
                     && x.AssigneeCompanyId == companyId,
                cancellationToken);
    }

    public async Task<PendingTask?> GetFanOutTaskByCorrelationIdAndCompanyAsync(
        Guid correlationId,
        string activityId,
        Guid assigneeCompanyId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PendingTasks
            .FirstOrDefaultAsync(
                x => x.CorrelationId == correlationId
                     && x.ActivityId == activityId
                     && x.AssigneeCompanyId == assigneeCompanyId,
                cancellationToken);
    }

    public async Task<List<CompletedTask>> GetCompletedFanOutTasksByCorrelationIdAndCompanyAsync(
        Guid correlationId,
        Guid assigneeCompanyId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CompletedTasks
            .Where(x => x.CorrelationId == correlationId && x.AssigneeCompanyId == assigneeCompanyId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CompletedTask>> GetCompletedTasksByCorrelationIdForUserAsync(
        Guid correlationId,
        string username,
        IReadOnlyList<string> userGroups,
        string? userTeamId,
        Guid? callerCompanyId,
        CancellationToken cancellationToken = default)
    {
        // Load all completed tasks for this correlation then filter in-memory via PoolTaskAccess.
        // This avoids duplicating the candidate-set logic in SQL and keeps the method simple.
        // Expected row count per quotation is small (single-digit number of workflow steps).
        var all = await dbContext.CompletedTasks
            .Where(x => x.CorrelationId == correlationId)
            .ToListAsync(cancellationToken);

        return all
            .Where(t => PoolTaskAccess.IsOwner(
                t.AssignedTo,
                t.AssigneeCompanyId,
                userGroups,
                userTeamId,
                callerCompanyId,
                username))
            .ToList();
    }
}