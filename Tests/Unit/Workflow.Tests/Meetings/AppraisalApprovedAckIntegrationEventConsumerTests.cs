using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.Data;
using Workflow.Meetings.Configuration;
using Workflow.Meetings.EventHandlers;
using Workflow.Meetings.ReadModels;
using Workflow.Workflow.Repositories;
using Xunit;

namespace Workflow.Tests.Meetings;

public class AppraisalApprovedAckIntegrationEventConsumerTests
{
    private static WorkflowDbContext NewDb() =>
        new(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"ack-queue-{Guid.NewGuid()}").Options);

    private static IOptions<AcknowledgementGroupSettings> SettingsWithMapping(string committeeCode, string group)
    {
        var settings = new AcknowledgementGroupSettings();
        settings.AcknowledgementGroupByCommitteeCode[committeeCode] = group;
        return Options.Create(settings);
    }

    private static IOptions<AcknowledgementGroupSettings> EmptySettings() =>
        Options.Create(new AcknowledgementGroupSettings());

    /// <summary>
    /// Builds a consume context for the event.
    /// Pass <c>messageId: null</c> (default) to disable inbox-guard processing — both
    /// <see cref="InboxGuard{T}.TryClaimAsync"/> and <see cref="InboxGuard{T}.MarkAsProcessedAsync"/>
    /// are no-ops when messageId is null. This avoids raw-SQL calls
    /// (ExecuteSqlRawAsync) that do not work with the EF in-memory provider.
    /// </summary>
    private static ConsumeContext<AppraisalApprovedIntegrationEvent> BuildContext(
        AppraisalApprovedIntegrationEvent message,
        Guid? messageId = null)
    {
        var ctx = Substitute.For<ConsumeContext<AppraisalApprovedIntegrationEvent>>();
        ctx.Message.Returns(message);
        ctx.MessageId.Returns(messageId);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    /// <summary>
    /// Builds the consumer. The <paramref name="unitOfWork"/> substitute is wired to
    /// actually call <c>db.SaveChangesAsync()</c> so that in-memory state is persisted.
    /// </summary>
    private static AppraisalApprovedAckIntegrationEventConsumer BuildConsumer(
        WorkflowDbContext db,
        IWorkflowUnitOfWork unitOfWork,
        IOptions<AcknowledgementGroupSettings> settings)
    {
        // Wire the unit-of-work substitute to flush the real DbContext so in-memory
        // state is queryable after the consumer runs.
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(ci => db.SaveChangesAsync(ci.Arg<CancellationToken>()));

        var inboxGuard = new InboxGuard<WorkflowDbContext>(
            db, Substitute.For<ILogger<InboxGuard<WorkflowDbContext>>>());

        var dateTimeProvider = Substitute.For<Shared.Time.IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(DateTime.UtcNow);

        return new AppraisalApprovedAckIntegrationEventConsumer(
            Substitute.For<ILogger<AppraisalApprovedAckIntegrationEventConsumer>>(),
            db,
            unitOfWork,
            settings,
            dateTimeProvider,
            inboxGuard);
    }

    // =========================================================================
    // Happy path — committee code is in the mapping
    // =========================================================================

    [Fact]
    public async Task Consume_WhenCommitteeCodeInAckSettings_CreatesQueueItem()
    {
        await using var db = NewDb();
        var unitOfWork = Substitute.For<IWorkflowUnitOfWork>();
        var settings = SettingsWithMapping("SUB", "Group1");
        var consumer = BuildConsumer(db, unitOfWork, settings);

        var appraisalId = Guid.NewGuid();
        var committeeId = Guid.NewGuid();

        // null messageId disables inbox guard so MarkAsProcessedAsync (which uses raw SQL)
        // is a no-op. This test focuses on domain logic, not message deduplication.
        var ctx = BuildContext(new AppraisalApprovedIntegrationEvent
        {
            AppraisalId = appraisalId,
            CommitteeCode = "SUB",
            CommitteeId = committeeId,
            AppraisalNo = "2568-0001",
            ApprovedAt = DateTime.UtcNow,
            ApprovedBy = "admin"
        });

        await consumer.Consume(ctx);

        var item = await db.AppraisalAcknowledgementQueueItems
            .AsNoTracking()
            .SingleOrDefaultAsync();

        item.Should().NotBeNull();
        item!.AppraisalId.Should().Be(appraisalId);
        item.CommitteeId.Should().Be(committeeId);
        item.CommitteeCode.Should().Be("SUB");
        item.AppraisalNo.Should().Be("2568-0001");
        item.AppraisalDecisionId.Should().BeNull("the consumer never has an AppraisalDecisionId");
        item.AcknowledgementGroup.Should().Be("Group1");
        item.Status.Should().Be(AcknowledgementStatus.PendingAcknowledgement);

        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Silent skip — committee code not in mapping
    // =========================================================================

    [Fact]
    public async Task Consume_WhenCommitteeCodeNotInAckSettings_SkipsSilently()
    {
        await using var db = NewDb();
        var unitOfWork = Substitute.For<IWorkflowUnitOfWork>();
        var settings = EmptySettings(); // no mapping at all
        var consumer = BuildConsumer(db, unitOfWork, settings);

        var ctx = BuildContext(new AppraisalApprovedIntegrationEvent
        {
            AppraisalId = Guid.NewGuid(),
            CommitteeCode = "MAIN",
            CommitteeId = Guid.NewGuid(),
            ApprovedAt = DateTime.UtcNow
        });

        await consumer.Consume(ctx);

        var count = await db.AppraisalAcknowledgementQueueItems.CountAsync();
        count.Should().Be(0, "a committee with no ack mapping should not produce a queue item");
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Domain-level idempotency — queue item already exists for same appraisal+committee
    // =========================================================================

    [Fact]
    public async Task Consume_WhenAlreadyEnqueued_SkipsIdempotently()
    {
        await using var db = NewDb();
        var unitOfWork = Substitute.For<IWorkflowUnitOfWork>();
        var settings = SettingsWithMapping("SUB", "Group1");
        var consumer = BuildConsumer(db, unitOfWork, settings);

        var appraisalId = Guid.NewGuid();
        var committeeId = Guid.NewGuid();

        // Pre-insert an existing active queue item
        var existing = AppraisalAcknowledgementQueueItem.Create(
            appraisalId, "2568-0001", null, committeeId, "SUB", "Group1", DateTime.UtcNow);
        db.AppraisalAcknowledgementQueueItems.Add(existing);
        await db.SaveChangesAsync();

        var ctx = BuildContext(new AppraisalApprovedIntegrationEvent
        {
            AppraisalId = appraisalId,
            CommitteeCode = "SUB",
            CommitteeId = committeeId,
            ApprovedAt = DateTime.UtcNow
        });

        await consumer.Consume(ctx);

        var count = await db.AppraisalAcknowledgementQueueItems.AsNoTracking().CountAsync();
        count.Should().Be(1, "duplicate event should not create a second queue item");
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Inbox guard deduplication — duplicate message ID short-circuits before domain logic
    //
    // NOTE: The real InboxGuard.TryClaimAsync uses a PK-conflict-based insert pattern.
    // The EF in-memory provider does not throw DbUpdateException on PK conflicts (it
    // throws ArgumentException), so the inbox guard's catch block is not triggered and
    // the deduplication path cannot be exercised with in-memory EF.
    //
    // This is a known limitation shared by the existing
    // AppraisalCreatedIntegrationEventConsumerTests in this project.
    // Integration-level coverage (via a real SQL Server) is the appropriate venue for
    // testing inbox-guard deduplication. The consumer's domain-level idempotency
    // (Consume_WhenAlreadyEnqueued_SkipsIdempotently) is tested above.
    // =========================================================================

    [Fact(Skip = "InboxGuard deduplication requires a relational DB provider. " +
                 "EF in-memory throws ArgumentException instead of DbUpdateException on PK conflict, " +
                 "bypassing the catch block in TryClaimAsync. " +
                 "Use domain-level idempotency test (Consume_WhenAlreadyEnqueued_SkipsIdempotently) instead.")]
    public async Task Consume_WhenMessageIdDuplicate_InboxGuardShortCircuits()
    {
        // Skipped — see class-level NOTE above.
        await Task.CompletedTask;
    }
}
