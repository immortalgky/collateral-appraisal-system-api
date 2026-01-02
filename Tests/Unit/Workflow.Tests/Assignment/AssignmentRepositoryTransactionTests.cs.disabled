using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Workflow.Data;
using Workflow.Data.Repository;
using Xunit;

namespace Workflow.Tests.Assignment;

public class AssignmentRepositoryTransactionTests
{
    private sealed class StubSqlConnectionFactory : ISqlConnectionFactory
    {
        public System.Data.IDbConnection GetOpenConnection() => throw new NotSupportedException();
        public System.Data.IDbConnection CreateNewConnection() => throw new NotSupportedException();
        public string GetConnectionString() => string.Empty;
    }

    private static WorkflowDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WorkflowDbContext(options);
    }

    [Fact]
    public async Task SyncUsers_Should_Throw_Without_Ambient_Transaction()
    {
        using var context = CreateInMemoryContext();
        var repo = new AssignmentRepository(context, new StubSqlConnectionFactory());

        var act = async () => await repo.SyncUsersForGroupCombinationAsync(
            "ActivityA",
            "hash123",
            "GroupA,GroupB",
            ["user1", "user2"],
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Ambient transaction required*");
    }

    [Fact]
    public async Task SelectNext_Should_Throw_Without_Ambient_Transaction()
    {
        using var context = CreateInMemoryContext();
        var repo = new AssignmentRepository(context, new StubSqlConnectionFactory());

        var act = async () => await repo.SelectNextUserWithRoundResetAsync(
            "ActivityA",
            "hash123",
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Ambient transaction required*");
    }
}

