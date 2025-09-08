using System.Data;
using Dapper;
using FluentAssertions;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;
using Integration.Helpers;
using Workflow.Data;
using Workflow.Data.Repository;
using Workflow.Tasks.Models;
using Xunit;

namespace Integration.Workflow.Integration.Tests.Assignment;

[Collection("Integration")]
public class RoundRobinStoredProcedureTests(IntegrationTestFixture fixture)
{
    private const string Activity = "assignment-int-test";
    private const string GroupsHash = "grp#A-B";
    private const string GroupsList = "A,B";

    [Fact]
    public async Task SelectNext_BasicOrdering_IncrementsAndReturnsSmallestUser()
    {
        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<IAssignmentRepository>();

        await CleanupAsync(dbContext);
        await SeedAsync(dbContext, ["user1", "user2", "user3"], new[] { 0, 0, 0 });

        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var selected = await repo.SelectNextUserWithRoundResetAsync(Activity, GroupsHash, default);
        await tx.CommitAsync();

        selected.Should().Be("user1");

        var rows = await dbContext.RoundRobinQueue
            .Where(x => x.ActivityName == Activity && x.GroupsHash == GroupsHash)
            .OrderBy(x => x.UserId)
            .ToListAsync();
        rows[0].AssignmentCount.Should().Be(1);
        rows[1].AssignmentCount.Should().Be(0);
        rows[2].AssignmentCount.Should().Be(0);
    }

    [Fact]
    public async Task SelectNext_AfterFullRound_ResetsAllCounts()
    {
        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<IAssignmentRepository>();

        await CleanupAsync(dbContext);
        await SeedAsync(dbContext, ["user1", "user2", "user3"], new[] { 1, 1, 1 });

        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var selected = await repo.SelectNextUserWithRoundResetAsync(Activity, GroupsHash, default);
        await tx.CommitAsync();

        selected.Should().Be("user1");

        var rows = await dbContext.RoundRobinQueue
            .Where(x => x.ActivityName == Activity && x.GroupsHash == GroupsHash)
            .OrderBy(x => x.UserId)
            .ToListAsync();

        // After increment and reset, all should be zero based on current SP semantics
        rows.Should().OnlyContain(r => r.AssignmentCount == 0);
    }

    [Fact]
    public async Task SelectNext_SkipsInactiveUsers()
    {
        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<IAssignmentRepository>();

        await CleanupAsync(dbContext);
        await SeedAsync(dbContext, ["user1", "user2", "user3"], new[] { 0, 0, 0 }, inactive: ["user1"]);

        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var selected = await repo.SelectNextUserWithRoundResetAsync(Activity, GroupsHash, default);
        await tx.CommitAsync();

        selected.Should().Be("user2");
    }

    [Fact]
    public async Task SelectionIndex_Should_Exist()
    {
        using var scope = fixture.Services.CreateScope();
        var connFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        using var conn = connFactory.GetOpenConnection();

        var count = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(1) FROM sys.indexes WHERE name = @name AND object_id = OBJECT_ID('[workflow].[RoundRobinQueue]')",
            new { name = "IX_RoundRobinQueue_Selection_Strict" }
        );

        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ConcurrentSelections_TenTasks_NoDeadlocks_AllComplete()
    {
        // Arrange
        var connFactory = fixture.Services.GetRequiredService<ISqlConnectionFactory>();
        await RoundRobinTestHelper.CleanupRoundRobinAsync(connFactory, Activity, GroupsHash);

        await using var scopeSeed = fixture.Services.CreateAsyncScope();
        var seedDb = scopeSeed.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        await SeedAsync(seedDb, ["user1", "user2", "user3"], new[] { 0, 0, 0 });

        // Act - run 10 parallel selections across separate scopes to simulate true concurrency
        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            var repo = scope.ServiceProvider.GetRequiredService<IAssignmentRepository>();
            await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var user = await repo.SelectNextUserWithRoundResetAsync(Activity, GroupsHash, default);
            await tx.CommitAsync();
            return user!;
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - all tasks completed without deadlock/exception and return only known users
        results.Should().HaveCount(10);
        results.Should().OnlyContain(u => u == "user1" || u == "user2" || u == "user3");
    }

    [Fact]
    public async Task ConcurrentSelections_TwoOnThreeUsers_ReturnsDistinctUsersNoReset()
    {
        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<IAssignmentRepository>();

        await CleanupAsync(dbContext);
        await SeedAsync(dbContext, ["user1", "user2", "user3"], new[] { 0, 0, 0 });

        var t1 = Task.Run(async () =>
        {
            await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var selected = await repo.SelectNextUserWithRoundResetAsync(Activity, GroupsHash, default);
            await tx.CommitAsync();
            return selected!;
        });

        var t2 = Task.Run(async () =>
        {
            await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var selected = await repo.SelectNextUserWithRoundResetAsync(Activity, GroupsHash, default);
            await tx.CommitAsync();
            return selected!;
        });

        var results = await Task.WhenAll(t1, t2);
        results.Should().OnlyHaveUniqueItems();

        var rows = await dbContext.RoundRobinQueue
            .Where(x => x.ActivityName == Activity && x.GroupsHash == GroupsHash)
            .ToListAsync();

        var sum = rows.Sum(r => r.AssignmentCount);
        sum.Should().Be(2); // no reset should occur since at least one zero remains
        rows.Count(r => r.AssignmentCount == 1).Should().Be(2);
        rows.Count(r => r.AssignmentCount == 0).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentSelections_ThreeOnThreeUsers_ReturnsAllDistinct()
    {
        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<IAssignmentRepository>();

        await CleanupAsync(dbContext);
        await SeedAsync(dbContext, ["user1", "user2", "user3"], new[] { 0, 0, 0 });

        var tasks = Enumerable.Range(0, 3).Select(async _ =>
        {
            await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var selected = await repo.SelectNextUserWithRoundResetAsync(Activity, GroupsHash, default);
            await tx.CommitAsync();
            return selected!;
        }).ToArray();

        var results = await Task.WhenAll(tasks);
        results.Should().HaveCount(3);
        results.Should().OnlyHaveUniqueItems();

        var rows = await dbContext.RoundRobinQueue
            .Where(x => x.ActivityName == Activity && x.GroupsHash == GroupsHash)
            .ToListAsync();

        // After concurrent selections, counts are either all 1 (no reset visible) or all 0 (reset visible)
        rows.All(r => r.AssignmentCount == 0 || r.AssignmentCount == 1).Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentSelections_SixOnThreeUsers_EachUserSelectedTwice()
    {
        // Arrange
        await using var scopeRoot = fixture.Services.CreateAsyncScope();
        var rootDb = scopeRoot.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        await CleanupAsync(rootDb);
        await SeedAsync(rootDb, ["user1", "user2", "user3"], new[] { 0, 0, 0 });

        // Act - run 6 parallel selections, each with its own scope/context/transaction
        var tasks = Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            var repo = scope.ServiceProvider.GetRequiredService<IAssignmentRepository>();
            await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var selected = await repo.SelectNextUserWithRoundResetAsync(Activity, GroupsHash, default)!;
            await tx.CommitAsync();
            return selected;
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - each user should be selected exactly twice across 6 selections
        results.Should().HaveCount(6);
        results.Count(u => u == "user1").Should().Be(2);
        results.Count(u => u == "user2").Should().Be(2);
        results.Count(u => u == "user3").Should().Be(2);
    }

    [Fact]
    public async Task SyncUsers_AddReactivateDeactivate_BehavesAsExpected()
    {
        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<IAssignmentRepository>();

        await CleanupAsync(dbContext);

        // Seed: user1 (count=2, active), user2 (count=1, active)
        await SeedAsync(dbContext, ["user1", "user2"], new[] { 2, 1 });

        // Sync: keep user2, add user3 -> user1 should be deactivated, user2 stays active (count preserved), user3 added active with count 0
        await using (var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            await repo.SyncUsersForGroupCombinationAsync(Activity, GroupsHash, GroupsList, ["user2", "user3"], default);
            await tx.CommitAsync();
        }

        var rows = await dbContext.RoundRobinQueue
            .Where(x => x.ActivityName == Activity && x.GroupsHash == GroupsHash)
            .OrderBy(x => x.UserId)
            .ToListAsync();

        rows.Should().HaveCount(3);
        var u1 = rows.Single(x => x.UserId == "user1");
        var u2 = rows.Single(x => x.UserId == "user2");
        var u3 = rows.Single(x => x.UserId == "user3");

        u1.IsActive.Should().BeFalse();
        u1.AssignmentCount.Should().Be(2);

        u2.IsActive.Should().BeTrue();
        u2.AssignmentCount.Should().Be(1);

        u3.IsActive.Should().BeTrue();
        u3.AssignmentCount.Should().Be(0);
        u3.GroupsList.Should().Be(GroupsList);
    }

    private static async Task CleanupAsync(WorkflowDbContext db)
    {
        var existing = await db.RoundRobinQueue
            .Where(x => x.ActivityName == Activity && x.GroupsHash == GroupsHash)
            .ToListAsync();
        db.RoundRobinQueue.RemoveRange(existing);
        await db.SaveChangesAsync();
    }

    private static async Task SeedAsync(WorkflowDbContext db, IReadOnlyList<string> users, IReadOnlyList<int> counts, IReadOnlyList<string>? inactive = null)
    {
        for (var i = 0; i < users.Count; i++)
        {
            db.RoundRobinQueue.Add(new RoundRobinQueue
            {
                ActivityName = Activity,
                GroupsHash = GroupsHash,
                GroupsList = GroupsList,
                UserId = users[i],
                AssignmentCount = counts[i],
                LastAssignedAt = DateTime.UtcNow,
                IsActive = inactive == null || !inactive.Contains(users[i])
            });
        }

        await db.SaveChangesAsync();
    }
}
