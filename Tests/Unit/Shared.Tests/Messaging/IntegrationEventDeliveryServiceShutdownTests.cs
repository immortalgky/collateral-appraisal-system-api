using System.Text.Json;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RabbitMQ.Client.Exceptions;
using Shared.Configurations;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Messaging.Services;
using Shared.Time;

namespace Shared.Tests.Messaging;

/// <summary>Fake host lifetime, only needed to satisfy the constructor — ProcessBatchAsync itself takes a
/// single already-combined token and never looks at IHostApplicationLifetime directly.</summary>
public class FakeHostApplicationLifetime : IHostApplicationLifetime
{
    public CancellationToken ApplicationStarted { get; set; } = CancellationToken.None;
    public CancellationToken ApplicationStopping { get; set; } = CancellationToken.None;
    public CancellationToken ApplicationStopped { get; set; } = CancellationToken.None;
    public void StopApplication() { }
}

/// <summary>
/// The same cases as the base outbox fix's IntegrationEventDeliveryServiceTests, adapted to this PR's
/// constructor (required <see cref="IHostApplicationLifetime"/>) and its extra
/// MessageId publish callback (<c>bus.Publish(..., IPipe&lt;PublishContext&gt;, ...)</c> instead of the
/// bare 3-arg overload). Drives <see cref="IntegrationEventDeliveryService{TDbContext}.ProcessBatchAsync"/>
/// directly against an EF Core InMemory database, same pattern as
/// <see cref="IntegrationEventDeliveryServiceMessageIdTests"/>.
/// </summary>
public class IntegrationEventDeliveryServiceShutdownTests
{
    public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.ApplyConfiguration(new IntegrationEventOutboxConfiguration());
    }

    /// <summary>Records every CancellationToken a save was actually invoked with, so a test can
    /// assert a healthy (non-cancelled) save used CancellationToken.None rather than a bounded token.</summary>
    public class RecordingTestDbContext(DbContextOptions<TestDbContext> options) : TestDbContext(options)
    {
        public List<CancellationToken> SaveTokens { get; } = [];

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveTokens.Add(cancellationToken);
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static DbContextOptions<TestDbContext> NewDbOptions() =>
        new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private static TestDbContext NewDb() => new(NewDbOptions());

    private static IntegrationEventDeliveryService<TestDbContext> NewService(
        IDateTimeProvider dateTimeProvider, IHostApplicationLifetime? lifetime = null, int? batchSize = null,
        TimeSpan? publishTimeout = null)
    {
        var options = new BackgroundJobsOptions();
        if (batchSize.HasValue)
            options.OutboxDelivery.BatchSize = batchSize.Value;
        if (publishTimeout.HasValue)
            options.OutboxDelivery.PublishTimeout = publishTimeout.Value;

        return new IntegrationEventDeliveryService<TestDbContext>(
            Substitute.For<IServiceScopeFactory>(),
            Substitute.For<ILogger<IntegrationEventDeliveryService<TestDbContext>>>(),
            dateTimeProvider,
            Options.Create(options),
            lifetime ?? new FakeHostApplicationLifetime());
    }

    private static IntegrationEventOutboxMessage ValidMessage(DateTime occurredAt, string correlationId) =>
        IntegrationEventOutboxMessage.Create(
            typeof(AssignmentSlaRecalculatedIntegrationEvent).AssemblyQualifiedName!,
            JsonSerializer.Serialize(new AssignmentSlaRecalculatedIntegrationEvent(), SerializerOptions),
            occurredAt,
            correlationId);

    /// <summary>Every test's bus stub uses this same 4-arg shape — PR-B's extra MessageId callback turns
    /// into an <c>IPipe&lt;PublishContext&gt;</c> under MassTransit's own Publish extension methods.</summary>
    private static IBus NewBus() => Substitute.For<IBus>();

    /// <summary>
    /// REGRESSION: when the first message of a correlation group fails to publish, the rest of that
    /// group must not be left stuck in <c>Processing</c> — it must go back to <c>Pending</c> so the
    /// next poll retries it, instead of waiting for the once-a-day cleanup job.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_FirstMessageInGroupThrows_RestOfGroupGoesBackToPending()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var first = ValidMessage(occurredAt, "group-1");
        var second = ValidMessage(occurredAt.AddSeconds(1), "group-1");
        db.Set<IntegrationEventOutboxMessage>().AddRange(first, second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var callCount = 0;
        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("publish boom");
                return Task.CompletedTask;
            });

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt);

        var service = NewService(dateTimeProvider);

        var processed = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);

        processed.Should().Be(0);
        callCount.Should().Be(1, "the batch must stop after the first failure, not keep publishing");

        var rows = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().OnlyContain(m => m.Status != OutboxMessageStatus.Processing);
        rows.Should().OnlyContain(m => m.ProcessingStartedAt == null);

        var reloadedFirst = rows.Single(m => m.Id == first.Id);
        reloadedFirst.Status.Should().Be(OutboxMessageStatus.Pending);
        reloadedFirst.RetryCount.Should().Be(1, "it went through IncrementRetryCount");

        var reloadedSecond = rows.Single(m => m.Id == second.Id);
        reloadedSecond.Status.Should().Be(OutboxMessageStatus.Pending);
        reloadedSecond.RetryCount.Should().Be(0, "it was never attempted — MarkAsPending, not a retry");
    }

    /// <summary>
    /// REGRESSION: a shutdown can surface as a non-<see cref="OperationCanceledException"/> — e.g.
    /// MassTransit's "bus stopping" or an <see cref="ObjectDisposedException"/> from the bus. That
    /// must be treated like cancellation (no retry burned, rethrown) rather than a delivery failure.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_NonOceExceptionDuringShutdown_RethrowsWithoutBurningRetry()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var first = ValidMessage(occurredAt, "group-1");
        db.Set<IntegrationEventOutboxMessage>().Add(first);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var cts = new CancellationTokenSource();
        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                // Simulate the bus being stopped concurrently with the publish call.
                cts.Cancel();
                throw new InvalidOperationException("bus stopping");
            });

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt);

        var service = NewService(dateTimeProvider);

        var act = async () => await service.ProcessBatchAsync(db, bus, cts.Token);

        await act.Should().ThrowAsync<InvalidOperationException>();

        var rows = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().OnlyContain(m => m.RetryCount == 0, "shutdown must not burn a retry");
        rows.Should().OnlyContain(m => m.Status == OutboxMessageStatus.Pending,
            "shutdown must put rows back to Pending, not leave them Processing");
        rows.Should().OnlyContain(m => m.ProcessingStartedAt == null);
    }

    /// <summary>
    /// REGRESSION: ExecuteAsync links stoppingToken with IHostApplicationLifetime.ApplicationStopping
    /// into a single token before calling ProcessBatchAsync, because MassTransit's hosted service
    /// (registered after this one) stops first on shutdown — a publish can fail with "bus stopping"
    /// while stoppingToken alone is still live. ProcessBatchAsync must treat cancellation on
    /// whichever token it's handed as shutdown, regardless of which source triggered it.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_ApplicationStoppingCancelledButStoppingTokenIsNot_TreatsBusStoppingAsShutdown()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var first = ValidMessage(occurredAt, "group-1");
        var second = ValidMessage(occurredAt.AddSeconds(1), "group-1");
        db.Set<IntegrationEventOutboxMessage>().AddRange(first, second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Simulate ExecuteAsync's linked token: stoppingToken never fires, only ApplicationStopping does.
        using var applicationStoppingCts = new CancellationTokenSource();
        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None, applicationStoppingCts.Token);

        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                // MassTransit's bus stops before our own stoppingToken is cancelled.
                applicationStoppingCts.Cancel();
                throw new InvalidOperationException("bus stopping");
            });

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt);

        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(applicationStoppingCts.Token);
        var service = NewService(dateTimeProvider, lifetime);

        var act = async () => await service.ProcessBatchAsync(db, bus, linkedCts.Token);

        await act.Should().ThrowAsync<InvalidOperationException>();

        var rows = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().OnlyContain(m => m.RetryCount == 0, "shutdown must not burn a retry");
        rows.Should().OnlyContain(m => m.Status == OutboxMessageStatus.Pending,
            "shutdown must put rows back to Pending, not leave them Processing");
        rows.Should().OnlyContain(m => m.ProcessingStartedAt == null);
    }

    /// <summary>
    /// REGRESSION: a message whose type resolves but is outside the allowed namespace is a
    /// deterministic failure — retrying can never help — so it goes straight to Failed with no
    /// retries burned, and processing continues with the rest of its group instead of abandoning it.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_DisallowedNamespaceAtHeadOfGroup_FailsHeadAndStillDeliversRestOfGroup()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Resolves fine (it's a real BCL type) but its namespace isn't the allowed one.
        var disallowed = IntegrationEventOutboxMessage.Create(
            typeof(string).AssemblyQualifiedName!, "\"x\"", occurredAt, correlationId: "group-1");
        var second = ValidMessage(occurredAt.AddSeconds(1), "group-1");
        db.Set<IntegrationEventOutboxMessage>().AddRange(disallowed, second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt);

        var service = NewService(dateTimeProvider);

        var processed = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);

        processed.Should().Be(1, "the second, valid message in the group is still delivered");
        await bus.Received(1).Publish(
            Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>());

        var rows = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);

        var reloadedHead = rows.Single(m => m.Id == disallowed.Id);
        reloadedHead.Status.Should().Be(OutboxMessageStatus.Failed);
        reloadedHead.RetryCount.Should().Be(0, "deterministic failures fail immediately, no retries burned");
        reloadedHead.Error.Should().StartWith(OutboxFailureReasons.Disallowed);
        reloadedHead.ProcessingStartedAt.Should().Be(occurredAt, "a Failed row keeps its last claim, which the purge ages it by");

        var reloadedSecond = rows.Single(m => m.Id == second.Id);
        reloadedSecond.Status.Should().Be(OutboxMessageStatus.Processed);
    }

    /// <summary>
    /// REGRESSION: an unresolvable type within the grace period looks like a
    /// rolling deploy — an old node holding the lease that doesn't yet know about a type only the
    /// new build defines — not a genuine failure. The whole group is held back this batch (not just
    /// the head) so a later message in the same correlation can't overtake it, and no retry is burned.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_UnresolvableTypeAtHeadOfGroup_WithinGracePeriod_HoldsWholeGroupPending()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var unresolvable = IntegrationEventOutboxMessage.Create(
            "Some.Unresolvable.Type, SomeAssembly", "{}", occurredAt, correlationId: "group-1");
        var second = ValidMessage(occurredAt.AddSeconds(1), "group-1");
        db.Set<IntegrationEventOutboxMessage>().AddRange(unresolvable, second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bus = NewBus();

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt); // age = 0, well within the grace period

        var service = NewService(dateTimeProvider);

        var processed = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);

        processed.Should().Be(0, "the whole group is held back, including the message after the unresolvable one");
        await bus.DidNotReceive().Publish(
            Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>());

        var rows = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().OnlyContain(m => m.Status == OutboxMessageStatus.Pending);
        rows.Should().OnlyContain(m => m.RetryCount == 0, "held within the grace period, not a retry");
    }

    /// <summary>
    /// REGRESSION: past the grace period, an unresolvable type is given up on —
    /// it goes straight to Failed with no retries burned, and the rest of its group still delivers.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_UnresolvableTypeAtHeadOfGroup_PastGracePeriod_FailsHeadAndStillDeliversRestOfGroup()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var unresolvable = IntegrationEventOutboxMessage.Create(
            "Some.Unresolvable.Type, SomeAssembly", "{}", occurredAt, correlationId: "group-1");
        var second = ValidMessage(occurredAt.AddSeconds(1), "group-1");
        db.Set<IntegrationEventOutboxMessage>().AddRange(unresolvable, second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt + OutboxDeliveryPolicy.VersionSkewGracePeriod
            + TimeSpan.FromMinutes(1)); // past the grace period

        var service = NewService(dateTimeProvider);

        var processed = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);

        processed.Should().Be(1, "the second, valid message in the group is still delivered");
        await bus.Received(1).Publish(
            Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>());

        var rows = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);

        var reloadedHead = rows.Single(m => m.Id == unresolvable.Id);
        reloadedHead.Status.Should().Be(OutboxMessageStatus.Failed);
        reloadedHead.RetryCount.Should().Be(0, "deterministic failures fail immediately, no retries burned");
        reloadedHead.Error.Should().StartWith(OutboxFailureReasons.Unresolvable);

        var reloadedSecond = rows.Single(m => m.Id == second.Id);
        reloadedSecond.Status.Should().Be(OutboxMessageStatus.Processed);
    }

    /// <summary>
    /// REGRESSION: a publish failure aborts the whole batch, not just the failing correlation
    /// group — the transport is probably down, so nothing else in the batch should be attempted.
    /// Only the message that actually failed burns a retry; a brief outage must not fail a whole
    /// chain of untried messages.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_PublishFailsForEveryMessage_AbortsWholeBatchAfterFirstFailure()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var messages = new[]
        {
            ValidMessage(occurredAt, "group-1"),
            ValidMessage(occurredAt.AddSeconds(1), "group-1"),
            ValidMessage(occurredAt.AddSeconds(2), "group-2"),
            ValidMessage(occurredAt.AddSeconds(3), "group-2")
        };
        db.Set<IntegrationEventOutboxMessage>().AddRange(messages);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("transport down"));

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt);

        var service = NewService(dateTimeProvider);

        var processed = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);

        processed.Should().Be(0);
        await bus.Received(1).Publish(
            Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>());

        var rows = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().OnlyContain(m => m.Status == OutboxMessageStatus.Pending);
        rows.Should().OnlyContain(m => m.ProcessingStartedAt == null);
        rows.Count(m => m.RetryCount == 1).Should().Be(1, "only the message that actually failed burns a retry");
        rows.Count(m => m.RetryCount == 0).Should().Be(3,
            "the rest of the batch — including the untried second group — goes back to Pending with no retry burned");
    }

    /// <summary>
    /// REGRESSION: ProcessBatchAsync must check the token before claiming a batch — a cancelled
    /// token must not flip any row to Processing that nothing will ever come back to reset.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_TokenAlreadyCancelled_ReturnsZeroWithoutClaimingAnyRows()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var message = ValidMessage(occurredAt, "group-1");
        db.Set<IntegrationEventOutboxMessage>().Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var bus = NewBus();
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt);

        var service = NewService(dateTimeProvider);

        var processed = await service.ProcessBatchAsync(db, bus, cts.Token);

        processed.Should().Be(0);
        await bus.DidNotReceive().Publish(
            Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>());

        var rows = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().OnlyContain(m => m.Status == OutboxMessageStatus.Pending,
            "a cancelled token must not claim the batch at all");
    }

    /// <summary>
    /// REGRESSION: a healthy save on a non-cancelled token must keep the old, unbounded
    /// (CancellationToken.None) behaviour — a merely slow database must not have a good save
    /// cancelled out from under it by a blanket shutdown-safe timeout.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_TokenNotCancelled_SavesUseCancellationTokenNone()
    {
        await using var db = new RecordingTestDbContext(NewDbOptions());
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var message = ValidMessage(occurredAt, "group-1");
        db.Set<IntegrationEventOutboxMessage>().Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.SaveTokens.Clear(); // drop the seed save above, only care about ProcessBatchAsync's own saves

        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt);

        var service = NewService(dateTimeProvider);

        var processed = await service.ProcessBatchAsync(db, bus, CancellationToken.None);

        processed.Should().Be(1);
        db.SaveTokens.Should().HaveCountGreaterThanOrEqualTo(2, "the claim save and the final save");
        db.SaveTokens.Should().OnlyContain(t => t == CancellationToken.None,
            "a non-cancelled token must never get a bounded/timeout token instead of None");
    }

    /// <summary>
    /// REGRESSION: a payload that no longer deserialises against its own type is version skew
    /// too — a property's shape can change mid-deploy just as easily as a whole type can appear.
    /// Within the grace period it's held (like an unresolvable type), not failed outright.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_DeserializeFailureAtHeadOfGroup_WithinGracePeriod_HoldsWholeGroupPending()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var malformed = IntegrationEventOutboxMessage.Create(
            typeof(AssignmentSlaRecalculatedIntegrationEvent).AssemblyQualifiedName!, "{ not valid json", occurredAt,
            correlationId: "group-1");
        var second = ValidMessage(occurredAt.AddSeconds(1), "group-1");
        db.Set<IntegrationEventOutboxMessage>().AddRange(malformed, second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bus = NewBus();

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt); // age = 0, well within the grace period

        var service = NewService(dateTimeProvider);

        var processed = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);

        processed.Should().Be(0, "the whole group is held back, including the message after the malformed one");
        await bus.DidNotReceive().Publish(
            Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>());

        var rows = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().OnlyContain(m => m.Status == OutboxMessageStatus.Pending);
        rows.Should().OnlyContain(m => m.RetryCount == 0, "held within the grace period, not a retry");
    }

    /// <summary>
    /// REGRESSION: past the grace period, a message that never deserialises is given up on,
    /// same as an unresolvable type — Failed, no retries burned, rest of the group still delivers.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_DeserializeFailureAtHeadOfGroup_PastGracePeriod_FailsHeadAndStillDeliversRestOfGroup()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var malformed = IntegrationEventOutboxMessage.Create(
            typeof(AssignmentSlaRecalculatedIntegrationEvent).AssemblyQualifiedName!, "{ not valid json", occurredAt,
            correlationId: "group-1");
        var second = ValidMessage(occurredAt.AddSeconds(1), "group-1");
        db.Set<IntegrationEventOutboxMessage>().AddRange(malformed, second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt + OutboxDeliveryPolicy.VersionSkewGracePeriod
            + TimeSpan.FromMinutes(1)); // past the grace period

        var service = NewService(dateTimeProvider);

        var processed = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);

        processed.Should().Be(1, "the second, valid message in the group is still delivered");
        await bus.Received(1).Publish(
            Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>());

        var rows = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);

        var reloadedHead = rows.Single(m => m.Id == malformed.Id);
        reloadedHead.Status.Should().Be(OutboxMessageStatus.Failed);
        reloadedHead.RetryCount.Should().Be(0, "deterministic failures fail immediately, no retries burned");
        reloadedHead.Error.Should().StartWith(OutboxFailureReasons.DeserializationFailed);

        var reloadedSecond = rows.Single(m => m.Id == second.Id);
        reloadedSecond.Status.Should().Be(OutboxMessageStatus.Processed);
    }

    /// <summary>
    /// REGRESSION: enough held (unresolvable-within-grace) rows to fill a whole batch must not
    /// starve a normal row that sorts after them by OccurredAt — the held-id exclusion is what lets
    /// the query skip past them once they're excluded, instead of re-claiming (and re-holding) the
    /// same rows on every single poll forever.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_MoreHeldRowsThanBatchSize_NormalRowStillProcessedWithinTwoCalls()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        const int batchSize = 5;

        // BatchSize + 1 held rows, each its own correlation group, all older than the normal row.
        var heldMessages = Enumerable.Range(0, batchSize + 1)
            .Select(i => IntegrationEventOutboxMessage.Create(
                "Some.Unresolvable.Type, SomeAssembly", "{}", occurredAt.AddSeconds(i), correlationId: $"held-{i}"))
            .ToList();
        var normal = ValidMessage(occurredAt.AddSeconds(batchSize + 10), "normal");

        db.Set<IntegrationEventOutboxMessage>().AddRange(heldMessages);
        db.Set<IntegrationEventOutboxMessage>().Add(normal);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt); // age = 0 for every held row, well within grace

        var service = NewService(dateTimeProvider, batchSize: batchSize);

        var firstBatch = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);
        firstBatch.Should().Be(0, "the first batch is entirely the earliest held rows");

        var secondBatch = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);
        secondBatch.Should().Be(1, "excluding the first batch's held ids finally brings the normal row into range");

        var reloadedNormal = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .SingleAsync(m => m.Id == normal.Id, TestContext.Current.CancellationToken);
        reloadedNormal.Status.Should().Be(OutboxMessageStatus.Processed);
    }

    /// <summary>
    /// REGRESSION: the broker being unreachable is not this message's fault — the batch is
    /// still aborted (transport is down for everyone), but the message itself must not burn a retry.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_TransportUnavailableException_AbortsBatchWithoutBurningRetry()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var first = ValidMessage(occurredAt, "group-1");
        var second = ValidMessage(occurredAt.AddSeconds(1), "group-2");
        db.Set<IntegrationEventOutboxMessage>().AddRange(first, second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new TransportUnavailableException("RabbitMQ endpoint not ready"));

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt);

        var service = NewService(dateTimeProvider);

        var processed = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);

        processed.Should().Be(0);
        await bus.Received(1).Publish(
            Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>());

        var rows = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().OnlyContain(m => m.Status == OutboxMessageStatus.Pending);
        rows.Should().OnlyContain(m => m.RetryCount == 0, "transport-unavailable must not burn a retry");
    }

    /// <summary>
    /// REGRESSION: the transport-unavailable classification must walk the InnerException chain,
    /// not just check the outer exception's type — MassTransit and RabbitMQ.Client both wrap the
    /// underlying connection failure inside other exception types.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_WrappedBrokerUnreachableException_AbortsBatchWithoutBurningRetry()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var first = ValidMessage(occurredAt, "group-1");
        db.Set<IntegrationEventOutboxMessage>().Add(first);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException(
                "publish failed", new BrokerUnreachableException(new Exception("connection refused"))));

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt);

        var service = NewService(dateTimeProvider);

        var processed = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);

        processed.Should().Be(0);

        var rows = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().OnlyContain(m => m.Status == OutboxMessageStatus.Pending);
        rows.Should().OnlyContain(m => m.RetryCount == 0,
            "a wrapped BrokerUnreachableException must still be recognised via the InnerException chain");
    }

    /// <summary>
    /// REGRESSION: holding must exclude the whole GROUP, not just the message that
    /// triggered the hold. Before this fix, a later message sharing the held head's CorrelationId —
    /// arriving after the head was already held — was excluded from nothing (its own Id was never
    /// added to the hold set) and would overtake the head it should have waited behind.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_LaterMessageSharesHeldCorrelationId_NotPublishedWhileHeadIsHeld()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var head = IntegrationEventOutboxMessage.Create(
            "Some.Unresolvable.Type, SomeAssembly", "{}", occurredAt, correlationId: "group-1");
        db.Set<IntegrationEventOutboxMessage>().Add(head);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt);

        var service = NewService(dateTimeProvider);

        // First poll: only the head exists; unresolvable-within-grace holds its (one-row) group.
        var firstBatch = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);
        firstBatch.Should().Be(0);

        // A later message for the SAME correlation arrives after the head is already held —
        // simulating a message published moments after the one that got stuck.
        var later = ValidMessage(occurredAt.AddSeconds(1), "group-1");
        db.Set<IntegrationEventOutboxMessage>().Add(later);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Second poll, still within the recheck interval: the later message must not be published —
        // it shares the held CorrelationId, so the whole group stays excluded, not just the head's Id.
        var secondBatch = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);
        secondBatch.Should().Be(0, "the later message's group is still held");
        await bus.DidNotReceive().Publish(
            Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>());

        var reloadedLater = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .SingleAsync(m => m.Id == later.Id, TestContext.Current.CancellationToken);
        reloadedLater.Status.Should().Be(OutboxMessageStatus.Pending);
    }

    /// <summary>
    /// REGRESSION: when the held-group cap is reached, evicting the soonest-due entry
    /// must release its WHOLE group at once (head included), not leave some of its rows excluded and
    /// others not. Proven by: once "first" is evicted, both its head (unresolvable, now also past its
    /// own grace period) and its tail (a normal, resolvable message) are acted on in the very next
    /// call — one Failed, one Processed — rather than one of them staying invisible to the query.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_HeldGroupCapReached_EvictsSoonestDueGroupEntirely()
    {
        await using var db = NewDb();
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var now = t0;
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(_ => now);
        var service = NewService(dateTimeProvider, batchSize: 600);

        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // "first": the very first group ever held, so its recheck-after time is the earliest of all —
        // guaranteed to be the one eviction picks once the cap is reached.
        var firstHead = IntegrationEventOutboxMessage.Create(
            "Some.Unresolvable.Type, SomeAssembly", "{}", t0, correlationId: "first");
        var firstTail = ValidMessage(t0.AddSeconds(1), "first");
        db.Set<IntegrationEventOutboxMessage>().AddRange(firstHead, firstTail);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        now = t0;
        (await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken)).Should().Be(0, "'first' is held");

        // 499 more single-message groups, each its own correlation, all held slightly later than
        // "first" — brings the held-group count to exactly OutboxDeliveryPolicy.MaxHeldMessages (500).
        var fillers = Enumerable.Range(0, 499)
            .Select(i => IntegrationEventOutboxMessage.Create(
                "Some.Unresolvable.Type, SomeAssembly", "{}", t0.AddSeconds(2), correlationId: $"filler-{i}"))
            .ToList();
        db.Set<IntegrationEventOutboxMessage>().AddRange(fillers);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        now = t0.AddSeconds(2);
        (await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken)).Should().Be(0, "all 499 fillers are held");

        // One more distinct group pushes the cap over the edge, evicting "first" (soonest-due).
        var late = IntegrationEventOutboxMessage.Create(
            "Some.Unresolvable.Type, SomeAssembly", "{}", t0.AddSeconds(3), correlationId: "late");
        db.Set<IntegrationEventOutboxMessage>().Add(late);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        now = t0.AddSeconds(3);
        (await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken)).Should().Be(0, "'late' is held, evicting 'first'");

        // Long past "first"'s own grace period (but everything else's much shorter recheck window
        // has also long since expired by now — irrelevant here, only "first" is asserted on).
        now = t0 + OutboxDeliveryPolicy.VersionSkewGracePeriod + TimeSpan.FromMinutes(5);
        var finalBatch = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);
        finalBatch.Should().Be(1, "only 'first's tail is a publishable message — everything else here is unresolvable");

        var rows = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .Where(m => m.Id == firstHead.Id || m.Id == firstTail.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        rows.Single(m => m.Id == firstHead.Id).Status.Should().Be(OutboxMessageStatus.Failed,
            "'first's head is unresolvable and, once no longer held, is now also past its own grace period");
        rows.Single(m => m.Id == firstTail.Id).Status.Should().Be(OutboxMessageStatus.Processed,
            "'first's tail is a normal message — released together with its head, not left behind");
    }

    /// <summary>
    /// REGRESSION: a channel-level soft error (bad routing/exchange arguments,
    /// access refused, etc.) is a problem with THIS message, not the broker — it must burn a retry
    /// like any other publish failure, not be retried forever as if the broker were unreachable.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_AlreadyClosedExceptionWithPreconditionFailed_BurnsRetryLikeAnyOtherFailure()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var message = ValidMessage(occurredAt, "group-1");
        db.Set<IntegrationEventOutboxMessage>().Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new AlreadyClosedException(
                new RabbitMQ.Client.Events.ShutdownEventArgs(
                    RabbitMQ.Client.ShutdownInitiator.Peer, RabbitMQ.Client.Constants.PreconditionFailed,
                    "PRECONDITION_FAILED - argument mismatch", "cause", CancellationToken.None)));

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt);

        var service = NewService(dateTimeProvider);

        var processed = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);

        processed.Should().Be(0);

        var row = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .SingleAsync(m => m.Id == message.Id, TestContext.Current.CancellationToken);
        row.Status.Should().Be(OutboxMessageStatus.Pending);
        row.RetryCount.Should().Be(1,
            "a 406 PRECONDITION_FAILED is this message's fault, not the broker's — it burns a retry");
    }

    /// <summary>
    /// REGRESSION: a connection-level close (no specific channel-level reply code)
    /// means the broker itself dropped the connection — treated as transport-unavailable, no retry.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_AlreadyClosedExceptionWithConnectionForced_TreatedAsTransportUnavailable()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var message = ValidMessage(occurredAt, "group-1");
        db.Set<IntegrationEventOutboxMessage>().Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new AlreadyClosedException(
                new RabbitMQ.Client.Events.ShutdownEventArgs(
                    RabbitMQ.Client.ShutdownInitiator.Library, RabbitMQ.Client.Constants.ConnectionForced,
                    "CONNECTION_FORCED", "cause", CancellationToken.None)));

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt);

        var service = NewService(dateTimeProvider);

        var processed = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);

        processed.Should().Be(0);

        var row = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .SingleAsync(m => m.Id == message.Id, TestContext.Current.CancellationToken);
        row.Status.Should().Be(OutboxMessageStatus.Pending);
        row.RetryCount.Should().Be(0,
            "a connection-level close (320 CONNECTION_FORCED) means the broker is unavailable, not this message's fault");
    }

    /// <summary>
    /// REGRESSION: MassTransit wraps a channel soft error as a
    /// <c>RabbitMqConnectionException</c> (a <see cref="ConnectionException"/>) whose inner
    /// exception is the AlreadyClosedException carrying 406. The outer type alone must not decide
    /// "transport unavailable" — the inner soft reply code says it is this message's fault, so it
    /// burns a retry and the poison message eventually goes Failed instead of stalling the outbox.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_ConnectionExceptionWrappingSoftErrorClose_BurnsRetry()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var message = ValidMessage(occurredAt, "group-1");
        db.Set<IntegrationEventOutboxMessage>().Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var inner = new AlreadyClosedException(
            new RabbitMQ.Client.Events.ShutdownEventArgs(
                RabbitMQ.Client.ShutdownInitiator.Peer, RabbitMQ.Client.Constants.PreconditionFailed,
                "PRECONDITION_FAILED - argument mismatch", "cause", CancellationToken.None));

        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new RabbitMqConnectionException("channel closed by broker", inner));

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt);

        var service = NewService(dateTimeProvider);

        var processed = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);

        processed.Should().Be(0);

        var row = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .SingleAsync(m => m.Id == message.Id, TestContext.Current.CancellationToken);
        row.Status.Should().Be(OutboxMessageStatus.Pending);
        row.RetryCount.Should().Be(1,
            "the inner 406 says the message is at fault even though the outer type is a ConnectionException");
    }

    /// <summary>
    /// REGRESSION: on the FIRST failed channel RPC (404/406) RabbitMQ.Client throws the BASE class
    /// <c>OperationInterruptedException</c>; only later calls on the already-closed channel throw the
    /// <c>AlreadyClosedException</c> subclass. MassTransit wraps the base class in a
    /// <c>RabbitMqConnectionException</c>, so matching only the subclass left the poison message
    /// looking like "transport unavailable" forever.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_ConnectionExceptionWrappingFirstFailedRpc_BurnsRetry()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var message = ValidMessage(occurredAt, "group-1");
        db.Set<IntegrationEventOutboxMessage>().Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var inner = new OperationInterruptedException(
            new RabbitMQ.Client.Events.ShutdownEventArgs(
                RabbitMQ.Client.ShutdownInitiator.Peer, RabbitMQ.Client.Constants.PreconditionFailed,
                "PRECONDITION_FAILED - inequivalent arg 'x-queue-type'", "cause", CancellationToken.None));

        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new RabbitMqConnectionException("channel RPC failed", inner));

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt);

        var service = NewService(dateTimeProvider);

        var processed = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);

        processed.Should().Be(0);

        var row = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .SingleAsync(m => m.Id == message.Id, TestContext.Current.CancellationToken);
        row.Status.Should().Be(OutboxMessageStatus.Pending);
        row.RetryCount.Should().Be(1,
            "the first failed RPC surfaces as the base OperationInterruptedException — a 406 is this message's fault");
    }

    // NOTE: there is no "null ShutdownReason" test because
    // RabbitMQ.Client.Exceptions.AlreadyClosedException's own constructor throws NullReferenceException
    // when given a null reason (verified empirically), so it can never be constructed with one. The
    // `{ ShutdownReason.ReplyCode: ... }` pattern in IsTransportUnavailable stays as defensive coding:
    // a null reason simply fails the soft-error match and falls through to the transport check.

    /// <summary>
    /// REGRESSION: MassTransit has no built-in publish timeout, so a broker that
    /// accepts the call but never acknowledges must not block this loop forever — bounded by
    /// BackgroundJobsOptions.OutboxDelivery.PublishTimeout, treated the same as the broker being
    /// unreachable. Passed in per-service (not a shared static), so this can't leak into other
    /// tests running in parallel. PR-B's 4-arg bus.Publish overload puts the CancellationToken at
    /// index 3 (after the IPipe&lt;PublishContext&gt;), not index 2 as in main-fork's 3-arg overload.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_PublishExceedsTimeout_AbortsBatchWithoutBurningRetry()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var message = ValidMessage(occurredAt, "group-1");
        db.Set<IntegrationEventOutboxMessage>().Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var publishToken = callInfo.ArgAt<CancellationToken>(3);
                // A broker that accepts the call but never acknowledges — only the per-publish
                // CancelAfter timeout ends this; the outer token here is never cancelled.
                await Task.Delay(Timeout.Infinite, publishToken);
            });

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt);

        var service = NewService(dateTimeProvider, publishTimeout: TimeSpan.FromMilliseconds(50));

        var processed = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);

        processed.Should().Be(0);

        var row = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .SingleAsync(m => m.Id == message.Id, TestContext.Current.CancellationToken);
        row.Status.Should().Be(OutboxMessageStatus.Pending);
        row.RetryCount.Should().Be(0, "a publish timeout is not this message's fault — no retry burned");
    }

    /// <summary>
    /// REGRESSION: PublishTimeout alone isn't enough — a batch of many merely-slow (not timed
    /// out) publishes can still add up past LeaseDuration. The lease must be renewed before each
    /// publish once a third of LeaseDuration has elapsed since it was last (re)acquired.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_ElapsedPastThirdOfLease_RenewsLeaseBeforePublish()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var message = ValidMessage(occurredAt, "group-1");
        db.Set<IntegrationEventOutboxMessage>().Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Simulates wall-clock time advancing during the batch: the first read establishes "now"
        // (and the lease-fresh baseline); each subsequent read is far enough ahead to cross
        // LeaseDuration/3 (10s at the default 30s LeaseDuration).
        var readCount = 0;
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(_ => occurredAt.AddSeconds(15 * readCount++));

        var service = NewService(dateTimeProvider);

        var renewCalls = 0;
        var processed = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken,
            (_, _) =>
            {
                renewCalls++;
                return Task.FromResult(true);
            });

        processed.Should().Be(1);
        renewCalls.Should().Be(1, "elapsed time since the lease was last (re)acquired exceeded a third of LeaseDuration");
        await bus.Received(1).Publish(
            Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>());

        var row = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .SingleAsync(m => m.Id == message.Id, TestContext.Current.CancellationToken);
        row.Status.Should().Be(OutboxMessageStatus.Processed);
    }

    /// <summary>
    /// REGRESSION: the renewal baseline is when the lease was actually stamped
    /// (before the claim query and anything else that ran in between), not when the batch method
    /// began. A lease already older than a third of LeaseDuration at the start of the batch must be
    /// renewed before the first publish even though the clock does not advance during the batch.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_LeaseStampedEarlierThanBatchStart_UsesStampAsRenewalBaseline()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var message = ValidMessage(occurredAt, "group-1");
        db.Set<IntegrationEventOutboxMessage>().Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(occurredAt);

        var service = NewService(dateTimeProvider);

        var renewCalls = 0;
        var processed = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken,
            renewLease: (_, _) =>
            {
                renewCalls++;
                return Task.FromResult(true);
            },
            leaseStampedAt: occurredAt.AddSeconds(-15));

        processed.Should().Be(1);
        renewCalls.Should().Be(1, "the lease was already 15s old (> LeaseDuration/3) when the batch began");
    }

    /// <summary>
    /// REGRESSION: if renewal reports another instance now owns the lease, we must stop
    /// touching these rows immediately — no retry burned, the message never even gets published.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_LeaseRenewalLost_AbortsBatchWithoutBurningRetry()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var message = ValidMessage(occurredAt, "group-1");
        db.Set<IntegrationEventOutboxMessage>().Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bus = NewBus();

        var readCount = 0;
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(_ => occurredAt.AddSeconds(15 * readCount++));

        var service = NewService(dateTimeProvider);

        var processed = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken,
            (_, _) => Task.FromResult(false));

        processed.Should().Be(0);
        await bus.DidNotReceive().Publish(
            Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>());

        var row = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .SingleAsync(m => m.Id == message.Id, TestContext.Current.CancellationToken);
        row.Status.Should().Be(OutboxMessageStatus.Pending);
        row.RetryCount.Should().Be(0, "losing the lease is not this message's fault — no retry burned");
    }

    /// <summary>
    /// REGRESSION: a renewal DB failure is indistinguishable from actually losing the lease —
    /// both must abort the batch without burning a retry, not be treated as a delivery failure.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_LeaseRenewalThrows_TreatedLikeLostLeaseWithoutBurningRetry()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var message = ValidMessage(occurredAt, "group-1");
        db.Set<IntegrationEventOutboxMessage>().Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bus = NewBus();

        var readCount = 0;
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(_ => occurredAt.AddSeconds(15 * readCount++));

        var service = NewService(dateTimeProvider);

        var processed = await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken,
            (_, _) => throw new InvalidOperationException("lease table unavailable"));

        processed.Should().Be(0);
        await bus.DidNotReceive().Publish(
            Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>());

        var row = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .SingleAsync(m => m.Id == message.Id, TestContext.Current.CancellationToken);
        row.Status.Should().Be(OutboxMessageStatus.Pending);
        row.RetryCount.Should().Be(0,
            "a renewal DB failure is indistinguishable from losing the lease — no retry burned");
    }

    // ---- Transport suspects: a timeout / transport failure burns a retry only when the message is
    // provably at fault (another publish succeeded earlier in the same batch). ----

    private static IntegrationEventOutboxMessage MessageWithId(Guid id, DateTime occurredAt, string correlationId) =>
        IntegrationEventOutboxMessage.Create(
            typeof(AssignmentSlaRecalculatedIntegrationEvent).AssemblyQualifiedName!,
            JsonSerializer.Serialize(new AssignmentSlaRecalculatedIntegrationEvent { AppraisalId = id },
                SerializerOptions),
            occurredAt,
            correlationId);

    /// <summary>Records the AppraisalId of every acknowledged publish in order. A publish whose id
    /// <paramref name="hangs"/> accepts is never acknowledged — only the per-publish timeout ends it.</summary>
    private static IBus RecordingBus(List<Guid> published, Func<Guid, bool> hangs)
    {
        var bus = NewBus();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var id = ((AssignmentSlaRecalculatedIntegrationEvent)callInfo.ArgAt<object>(0)).AppraisalId;
                if (hangs(id))
                    await Task.Delay(Timeout.Infinite, callInfo.ArgAt<CancellationToken>(3));
                published.Add(id);
            });
        return bus;
    }

    private static IDateTimeProvider FixedClock(DateTime now)
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(now);
        return dateTimeProvider;
    }

    /// <summary>
    /// Broker down: every publish times out, so nothing succeeds before any timeout. No row may burn a
    /// retry, however many polls pass and however the suspect ordering reshuffles the groups.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_BrokerDownEveryPublishTimesOut_NoRetryBurnedOnAnyRow()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ct = TestContext.Current.CancellationToken;

        var first = ValidMessage(occurredAt, "group-1");
        var second = ValidMessage(occurredAt.AddSeconds(1), "group-2");
        db.Set<IntegrationEventOutboxMessage>().AddRange(first, second);
        await db.SaveChangesAsync(ct);

        var bus = RecordingBus([], _ => true);
        var service = NewService(FixedClock(occurredAt), publishTimeout: TimeSpan.FromMilliseconds(50));

        for (var poll = 0; poll < 3; poll++)
            (await service.ProcessBatchAsync(db, bus, ct)).Should().Be(0);

        var rows = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking().ToListAsync(ct);
        rows.Should().OnlyContain(m => m.Status == OutboxMessageStatus.Pending);
        rows.Should().OnlyContain(m => m.RetryCount == 0, "a broker-down timeout is nobody's fault");
    }

    /// <summary>
    /// Poison message: M times out every time because of itself. Once M is a suspect it is processed
    /// LAST, so another group publishes first in the batch — proof the broker works — and M's timeout
    /// then burns a retry. After MaxRetries such polls it reaches Failed and stops blocking the outbox.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_SuspectTimesOutAfterAnotherGroupPublished_BurnsRetryUntilFailed()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ct = TestContext.Current.CancellationToken;
        const int maxRetries = 5; // BackgroundJobsOptions.OutboxDelivery.MaxRetries default

        var poisonId = Guid.NewGuid();
        var poison = MessageWithId(poisonId, occurredAt, "group-poison");
        db.Set<IntegrationEventOutboxMessage>().Add(poison);

        var healthy = new List<IntegrationEventOutboxMessage>();
        var bus = RecordingBus([], id => id == poisonId);
        var service = NewService(FixedClock(occurredAt), publishTimeout: TimeSpan.FromMilliseconds(50));

        for (var poll = 0; poll <= maxRetries + 1; poll++)
        {
            var next = ValidMessage(occurredAt.AddSeconds(1 + poll), $"group-healthy-{poll}");
            healthy.Add(next);
            db.Set<IntegrationEventOutboxMessage>().Add(next);
            await db.SaveChangesAsync(ct);

            await service.ProcessBatchAsync(db, bus, ct);

            if (poll == 0)
            {
                poison.RetryCount.Should().Be(0,
                    "first poll: nothing succeeded before the timeout, so it looks like the broker being down");
                poison.Status.Should().Be(OutboxMessageStatus.Pending);
            }
        }

        poison.Status.Should().Be(OutboxMessageStatus.Failed, "it burned a retry on every poll after the first");
        poison.RetryCount.Should().Be(maxRetries);
        poison.Error.Should().Contain("timed out");
        healthy.Should().OnlyContain(m => m.Status == OutboxMessageStatus.Processed,
            "the poison message no longer blocks the groups behind it");
    }

    /// <summary>
    /// A suspect's whole group is moved behind the independent groups; the group itself stays intact and
    /// in order. Only the order of independent groups changes.
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_GroupContainsSuspect_ProcessedAfterOtherGroupsAndStaysInOrder()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ct = TestContext.Current.CancellationToken;

        var suspect1 = Guid.NewGuid();
        var suspect2 = Guid.NewGuid();
        var other = Guid.NewGuid();
        db.Set<IntegrationEventOutboxMessage>().AddRange(
            MessageWithId(suspect1, occurredAt, "group-suspect"),
            MessageWithId(suspect2, occurredAt.AddSeconds(1), "group-suspect"),
            MessageWithId(other, occurredAt.AddSeconds(2), "group-other"));
        await db.SaveChangesAsync(ct);

        var published = new List<Guid>();
        var hang = true;
        var bus = RecordingBus(published, id => hang && id == suspect1);
        var service = NewService(FixedClock(occurredAt), publishTimeout: TimeSpan.FromMilliseconds(50));

        await service.ProcessBatchAsync(db, bus, ct); // suspect1 times out, nothing succeeded
        published.Should().BeEmpty();

        hang = false;
        await service.ProcessBatchAsync(db, bus, ct);

        published.Should().Equal(other, suspect1, suspect2);
    }

    /// <summary>A suspect that later publishes is cleared: it sorts by age again, ahead of newer groups.</summary>
    [Fact]
    public async Task ProcessBatchAsync_SuspectLaterPublishes_IsNoLongerProcessedLast()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ct = TestContext.Current.CancellationToken;

        var suspectId = Guid.NewGuid();
        var suspect = MessageWithId(suspectId, occurredAt, "group-suspect");
        db.Set<IntegrationEventOutboxMessage>().Add(suspect);
        await db.SaveChangesAsync(ct);

        var published = new List<Guid>();
        var hang = true;
        var bus = RecordingBus(published, id => hang && id == suspectId);
        var service = NewService(FixedClock(occurredAt), publishTimeout: TimeSpan.FromMilliseconds(50));

        await service.ProcessBatchAsync(db, bus, ct); // times out: suspect
        hang = false;
        await service.ProcessBatchAsync(db, bus, ct); // publishes: cleared
        suspect.Status.Should().Be(OutboxMessageStatus.Processed);

        // Re-queue the same row next to a newer group. Were it still a suspect it would go last.
        var newerId = Guid.NewGuid();
        suspect.MarkAsPending();
        db.Set<IntegrationEventOutboxMessage>().Add(MessageWithId(newerId, occurredAt.AddSeconds(1), "group-newer"));
        await db.SaveChangesAsync(ct);
        published.Clear();

        await service.ProcessBatchAsync(db, bus, ct);

        published.Should().Equal(suspectId, newerId);
    }

    /// <summary>A payload that deserialises to null carries the DeserializationReturnedNull prefix.</summary>
    [Fact]
    public async Task ProcessBatchAsync_PayloadDeserialisesToNull_PastGracePeriod_FailsWithReturnedNullPrefix()
    {
        await using var db = NewDb();
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ct = TestContext.Current.CancellationToken;

        var nullPayload = IntegrationEventOutboxMessage.Create(
            typeof(AssignmentSlaRecalculatedIntegrationEvent).AssemblyQualifiedName!, "null", occurredAt,
            correlationId: "group-1");
        db.Set<IntegrationEventOutboxMessage>().Add(nullPayload);
        await db.SaveChangesAsync(ct);

        var service = NewService(FixedClock(occurredAt + OutboxDeliveryPolicy.VersionSkewGracePeriod
            + TimeSpan.FromMinutes(1)));

        await service.ProcessBatchAsync(db, NewBus(), ct);

        nullPayload.Status.Should().Be(OutboxMessageStatus.Failed);
        nullPayload.Error.Should().StartWith(OutboxFailureReasons.DeserializationReturnedNull);
    }

    /// <summary>The prefixes are a wire contract: rows already stored in the outbox carry the literals.</summary>
    [Fact]
    public void OutboxFailureReasons_KeepTheLiteralsAlreadyStoredInFailedRows()
    {
        OutboxFailureReasons.Disallowed.Should().Be("Disallowed type:");
        OutboxFailureReasons.Unresolvable.Should().Be("Unresolvable type:");
        OutboxFailureReasons.DeserializationFailed.Should().Be("Deserialization failed:");
        OutboxFailureReasons.DeserializationReturnedNull.Should().Be("Deserialization returned null");
    }
}
