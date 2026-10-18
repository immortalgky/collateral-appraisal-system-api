using FluentAssertions;
using Integration.Domain.FailedMessages;
using Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Integration.Tests;

/// <summary>
/// The Failed Messages screen polls the summary and the list every 15 s, so the index shapes the handlers'
/// SQL relies on are part of the contract, not an incidental detail. Reads the EF model only (no database).
/// </summary>
public class FailedMessageIndexModelTests
{
    private static IEntityType FailedMessageEntity()
    {
        var options = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseSqlServer("Server=unused;Database=unused").Options;
        using var db = new IntegrationDbContext(options);
        // SQL Server's INCLUDE columns are only kept in the design-time model.
        return db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(FailedMessage))!;
    }

    private static IIndex Index(string name) =>
        FailedMessageEntity().GetIndexes().Single(i => i.GetDatabaseName() == name);

    /// <summary>Covers the summary's three Pending GROUP BYs and the list's queue filter (no clustered scan
    /// of the ~10 KB-wide rows), and the list's search predicate (no key lookup per Pending row while it
    /// hunts for 20 matches).</summary>
    [Fact]
    public void StatusFaultedAtIndex_CoversSummaryGroupingsAndSearchColumns()
    {
        var index = Index("IX_FailedMessages_Status_FaultedAt");

        index.Properties.Select(p => p.Name).Should().Equal(nameof(FailedMessage.Status), nameof(FailedMessage.FaultedAt));
        index.GetIncludeProperties().Should().BeEquivalentTo(
        [
            nameof(FailedMessage.SourceQueue), nameof(FailedMessage.ExceptionType), nameof(FailedMessage.Node),
            nameof(FailedMessage.Kind), nameof(FailedMessage.MessageId), nameof(FailedMessage.RefNumber),
            nameof(FailedMessage.ExceptionMessage)
        ]);
    }

    /// <summary>Summary's last-24h Retried/Discarded counts, and FailedMessageCleanupJob's DELETE predicate.</summary>
    [Fact]
    public void StatusActionAtIndex_Exists()
    {
        Index("IX_FailedMessages_Status_ActionAt").Properties.Select(p => p.Name)
            .Should().Equal(nameof(FailedMessage.Status), nameof(FailedMessage.ActionAt));
    }
}
