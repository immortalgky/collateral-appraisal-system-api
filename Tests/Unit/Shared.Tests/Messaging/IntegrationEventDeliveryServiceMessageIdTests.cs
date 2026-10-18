using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shared.Configurations;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Messaging.Services;
using Shared.Time;

namespace Shared.Tests.Messaging;

/// <summary>
/// A <see cref="FailedMessage"/> row traces back to an outbox row via MessageId only
/// if the delivery service actually publishes with MessageId = the outbox row's own Id.
/// Drives <see cref="IntegrationEventDeliveryService{TDbContext}.ProcessBatchAsync"/> directly
/// against an EF Core InMemory database and a substitute <see cref="IBus"/> — no real database or
/// broker needed to prove this.
/// </summary>
public class IntegrationEventDeliveryServiceMessageIdTests
{
    public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.ApplyConfiguration(new IntegrationEventOutboxConfiguration());
    }

    [Fact]
    public async Task ProcessBatchAsync_PublishesWithMessageId_EqualToTheOutboxRowId()
    {
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new TestDbContext(dbOptions);

        var eventType = typeof(AssignmentSlaRecalculatedIntegrationEvent).AssemblyQualifiedName!;
        var message = IntegrationEventOutboxMessage.Create(eventType, "{}", DateTime.Now);
        db.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        IPipe<PublishContext>? capturedPipe = null;
        var bus = Substitute.For<IBus>();
        bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Do<IPipe<PublishContext>>(p => capturedPipe = p),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(DateTime.Now);

        var service = new IntegrationEventDeliveryService<TestDbContext>(
            Substitute.For<IServiceScopeFactory>(),
            Substitute.For<ILogger<IntegrationEventDeliveryService<TestDbContext>>>(),
            dateTimeProvider,
            Options.Create(new BackgroundJobsOptions()),
            Substitute.For<IHostApplicationLifetime>());

        await service.ProcessBatchAsync(db, bus, TestContext.Current.CancellationToken);

        capturedPipe.Should().NotBeNull("ProcessBatchAsync should have published the pending row");

        // The real MassTransit pipeline invokes this pipe against a real PublishContext; substituting
        // one here and sending it through is the cheapest way to observe what the callback actually did.
        var context = Substitute.For<PublishContext>();
        await capturedPipe!.Send(context);

        context.MessageId.Should().Be(message.Id);
    }
}
