using FluentAssertions;
using Integration.Infrastructure.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Integration.Tests;

/// <summary>
/// IX_FailedMessages_Status_FaultedAt is rebuilt in place with <c>DROP_EXISTING = ON</c> (one statement, never a
/// moment with no index). A separate DropIndex + CreateIndex would leave the summary — polled every 15 s —
/// scanning the ~10 KB-wide clustered rows in between. ONLINE is deliberately not used: the edition may not
/// support it.
/// </summary>
public class FailedMessagesIndexMigrationTests
{
    private const string Index = "IX_FailedMessages_Status_FaultedAt";

    private static readonly FailedMessagesSummaryAndSearchIndexes Migration = new();

    private static List<string> SqlOf(IReadOnlyList<MigrationOperation> operations) =>
        operations.OfType<SqlOperation>().Select(o => o.Sql).ToList();

    private static void ShouldNotDropOrCreateTheIndexSeparately(IReadOnlyList<MigrationOperation> operations)
    {
        operations.OfType<DropIndexOperation>().Select(o => o.Name).Should().NotContain(Index);
        operations.OfType<CreateIndexOperation>().Select(o => o.Name).Should().NotContain(Index);
    }

    [Fact]
    public void Up_RebuildsStatusFaultedAtInPlace_WithDropExisting_AndKeepsTheActionAtCreate()
    {
        ShouldNotDropOrCreateTheIndexSeparately(Migration.UpOperations);

        var sql = SqlOf(Migration.UpOperations).Should().ContainSingle().Subject;
        sql.Should().Contain($"CREATE NONCLUSTERED INDEX [{Index}]")
            .And.Contain("ON [integration].[FailedMessages] ([Status], [FaultedAt])")
            .And.Contain(
                "INCLUDE ([SourceQueue], [ExceptionType], [Node], [Kind], [MessageId], [RefNumber], [ExceptionMessage])")
            .And.Contain("DROP_EXISTING = ON");
        sql.Should().NotContainEquivalentOf("ONLINE");

        Migration.UpOperations.OfType<CreateIndexOperation>().Should().ContainSingle()
            .Which.Name.Should().Be("IX_FailedMessages_Status_ActionAt");
    }

    [Fact]
    public void Down_RestoresThePlainIndexInPlace_AndDropsTheActionAtIndex()
    {
        ShouldNotDropOrCreateTheIndexSeparately(Migration.DownOperations);

        var sql = SqlOf(Migration.DownOperations).Should().ContainSingle().Subject;
        sql.Should().Contain($"CREATE NONCLUSTERED INDEX [{Index}]")
            .And.Contain("ON [integration].[FailedMessages] ([Status], [FaultedAt])")
            .And.Contain("DROP_EXISTING = ON");
        sql.Should().NotContainEquivalentOf("INCLUDE").And.NotContainEquivalentOf("ONLINE");

        Migration.DownOperations.OfType<DropIndexOperation>().Should().ContainSingle()
            .Which.Name.Should().Be("IX_FailedMessages_Status_ActionAt");
    }
}
