using FluentAssertions;
using Integration.Domain.FailedMessages;
using Integration.FailedMessages;
using Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shared.Messaging.Services;
using Shared.Time;

namespace Integration.Tests;

/// <summary>
/// An exception raised INSIDE <see cref="FailedMessageCollectorService.ResolveDuplicateAsync"/>
/// (a DB failure while resolving a dedup hit) must only nack that one message and let the caller keep
/// going — never escape and abort the whole Collect step. Forces the failure with a DbContext subclass
/// whose SaveChangesAsync throws only on the second call (the seed insert succeeds; the duplicate's
/// rebase-and-insert inside ResolveDuplicateAsync does not).
/// </summary>
public class FailedMessageResolveDuplicateFailureTests
{
    private sealed class ThrowsOnSecondSaveDbContext(DbContextOptions<IntegrationDbContext> options)
        : IntegrationDbContext(options)
    {
        private int _saveCount;

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            _saveCount++;
            if (_saveCount == 2)
                throw new InvalidOperationException("simulated DB failure inside ResolveDuplicateAsync");

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private static FailedMessageCollectorService CreateCollector(IDateTimeProvider dateTimeProvider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Host"] = "amqp://localhost:5672/",
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
            })
            .Build();

        return new FailedMessageCollectorService(
            Substitute.For<IServiceScopeFactory>(),
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<ILogger<FailedMessageCollectorService>>(),
            dateTimeProvider,
            Options.Create(new FailedMessagesOptions()),
            configuration,
            new ReceiveEndpointDiscoveryObserver());
    }

    [Fact]
    public async Task SafeResolveDuplicateAsync_UnderlyingSaveThrows_ReturnsNackAndStop_DoesNotThrow()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        dateTimeProvider.ApplicationNow.Returns(faultedAt.AddMinutes(5));
        var collector = CreateCollector(dateTimeProvider);

        var messageId = Guid.NewGuid();
        const string sourceQueue = "appraisal-sync";

        var options = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ThrowsOnSecondSaveDbContext(options);

        // Seed (save #1, succeeds) an already-Retried row sharing the dedup key, so ResolveDuplicateAsync
        // decides to insert the incoming duplicate rather than no-op.
        var original = FailedMessage.Create(
            "APP-NODE-01", sourceQueue, FailedMessageKind.Error, messageId, null, "Test.FakeEvent", null,
            "System.TimeoutException", "boom", null, 0, faultedAt, faultedAt, null, null, null,
            "{}"u8.ToArray(), "application/json", null);
        original.RequestRetry("test-actor", null, faultedAt);
        original.MarkRetried(faultedAt);
        db.FailedMessages.Add(original);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var duplicate = FailedMessage.Create(
            "APP-NODE-01", sourceQueue, FailedMessageKind.Error, messageId, null, "Test.FakeEvent", null,
            "System.TimeoutException", "boom", null, 0, faultedAt, faultedAt, null, null, null,
            "{}"u8.ToArray(), "application/json", null);

        // Save #2 (inside ResolveDuplicateAsync) throws — SafeResolveDuplicateAsync must catch it.
        var outcome = await collector.SafeResolveDuplicateAsync(
            db, duplicate, "appraisal-sync_error", TestContext.Current.CancellationToken);

        outcome.Should().Be(FailedMessageCollectorService.PersistOutcome.NackAndStop);

        // The original row is untouched; the exception did not corrupt or duplicate anything.
        var count = await db.FailedMessages.AsNoTracking().CountAsync(TestContext.Current.CancellationToken);
        count.Should().Be(1);
    }
}
