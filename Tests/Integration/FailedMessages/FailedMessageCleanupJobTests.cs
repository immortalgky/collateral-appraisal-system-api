using FluentAssertions;
using Integration.Domain.FailedMessages;
using Integration.Fixtures;
using Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.Time;

namespace Integration.FailedMessages;

/// <summary>
/// BrokerSnapshots are keyed by node and overwritten every round, so a decommissioned or renamed node's row
/// is never touched again. The daily cleanup job must age those out — against real SQL, because the delete is
/// raw SQL (EF InMemory can't run it).
/// </summary>
[Collection("Integration")]
public class FailedMessageCleanupJobTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task ExecuteAsync_DeletesBrokerSnapshotsNotCollectedForSevenDays_KeepsRecentOnes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var decommissioned = $"OLD-{suffix}";
        var nearlyStale = $"EDGE-{suffix}";
        var live = $"LIVE-{suffix}";

        db.BrokerSnapshots.AddRange(
            BrokerSnapshot.Create(decommissioned, now.AddDays(-7).AddMinutes(-1), BrokerManagementStatus.Ok, "[]", null),
            BrokerSnapshot.Create(nearlyStale, now.AddDays(-7).AddMinutes(1), BrokerManagementStatus.Ok, "[]", null),
            BrokerSnapshot.Create(live, now, BrokerManagementStatus.Ok, "[]", null));
        await db.SaveChangesAsync(ct);

        var clock = Substitute.For<IDateTimeProvider>();
        clock.ApplicationNow.Returns(now);
        var job = new FailedMessageCleanupJob(db, NullLogger<FailedMessageCleanupJob>.Instance, clock);

        await job.ExecuteAsync(ct);

        var remaining = await db.BrokerSnapshots.AsNoTracking()
            .Where(s => s.Node == decommissioned || s.Node == nearlyStale || s.Node == live)
            .Select(s => s.Node)
            .ToListAsync(ct);
        remaining.Should().BeEquivalentTo([nearlyStale, live]);
    }
}
