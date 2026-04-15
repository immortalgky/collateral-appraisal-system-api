using FluentAssertions;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.Data;
using Workflow.DocumentFollowups.Application;
using Workflow.DocumentFollowups.Domain;
using Workflow.DocumentFollowups.EventHandlers;
using Xunit;

namespace Workflow.Tests.DocumentFollowups;

/// <summary>
/// Integration-style tests (in-memory EF) for the document followup raise → upload → resolve flow.
/// Covers the <see cref="RequestDocumentLinkedConsumer"/> state-machine transitions and the
/// <see cref="DocumentFollowupGate"/> gate behaviour at each stage.
/// </summary>
public class RequestDocumentLinkedConsumerTests
{
    private static WorkflowDbContext NewDb() =>
        new(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"doclinked-{Guid.NewGuid()}").Options);

    private static RequestDocumentLinkedConsumer BuildConsumer(
        WorkflowDbContext db) =>
        new(
            db,
            Substitute.For<IPublisher>(),
            new InboxGuard<WorkflowDbContext>(db, Substitute.For<ILogger<InboxGuard<WorkflowDbContext>>>()),
            Substitute.For<ILogger<RequestDocumentLinkedConsumer>>());

    private static ConsumeContext<DocumentLinkedIntegrationEventV2> BuildContext(
        Guid requestId, string documentType, Guid? documentId = null)
    {
        var ctx = Substitute.For<ConsumeContext<DocumentLinkedIntegrationEventV2>>();
        ctx.Message.Returns(new DocumentLinkedIntegrationEventV2(
            requestId, documentId ?? Guid.NewGuid(), documentType));
        // Pass null MessageId so InboxGuard is a no-op (null-safe path in TryClaimAsync /
        // MarkAsProcessedAsync), keeping the in-memory EF provider happy (no ExecuteSqlRaw).
        ctx.MessageId.Returns((Guid?)null);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    private static DocumentFollowup RaiseFollowup(
        Guid requestId,
        Guid raisingPendingTaskId,
        params string[] documentTypes)
    {
        var lineItems = documentTypes.Select(t => (t, (string?)null));
        var followup = DocumentFollowup.Raise(
            appraisalId: Guid.NewGuid(),
            requestId: requestId,
            raisingWorkflowInstanceId: Guid.NewGuid(),
            raisingPendingTaskId: raisingPendingTaskId,
            raisingActivityId: "appraisal-initiation-check",
            raisingUserId: "checker-1",
            lineItems: lineItems);
        followup.AttachFollowupWorkflowInstance(Guid.NewGuid());
        return followup;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 1: Raise followup with 2 line items → gate blocks parent task
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RaisedFollowup_WithTwoLineItems_GateBlocksParentTask()
    {
        await using var db = NewDb();
        var taskId = Guid.NewGuid();
        var followup = RaiseFollowup(Guid.NewGuid(), taskId, "PassportCopy", "IncomeProof");
        db.DocumentFollowups.Add(followup);
        await db.SaveChangesAsync();

        var gate = new DocumentFollowupGate(db);
        var isBlocked = await gate.HasOpenFollowupAsync(taskId);

        followup.Status.Should().Be(DocumentFollowupStatus.Open);
        followup.LineItems.Should().HaveCount(2);
        followup.LineItems.Should().OnlyContain(li => li.Status == DocumentFollowupLineItemStatus.Pending);
        isBlocked.Should().BeTrue("gate must block parent task while followup is Open");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 2: Upload first document → first line item Uploaded, followup still Open
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FirstUpload_MatchingLineItem_FlipsToUploaded_FollowupStillOpen()
    {
        await using var db = NewDb();
        var requestId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var followup = RaiseFollowup(requestId, taskId, "PassportCopy", "IncomeProof");
        db.DocumentFollowups.Add(followup);
        await db.SaveChangesAsync();

        var consumer = BuildConsumer(db);
        var ctx = BuildContext(requestId, "PassportCopy");

        await consumer.Consume(ctx);

        var persisted = await db.DocumentFollowups
            .AsNoTracking()
            .FirstAsync(f => f.Id == followup.Id);

        persisted.Status.Should().Be(DocumentFollowupStatus.Open,
            "one pending item remains after first upload");
        persisted.LineItems.Single(li => li.DocumentType == "PassportCopy").Status
            .Should().Be(DocumentFollowupLineItemStatus.Uploaded);
        persisted.LineItems.Single(li => li.DocumentType == "IncomeProof").Status
            .Should().Be(DocumentFollowupLineItemStatus.Pending);

        // Gate still blocks
        var gate = new DocumentFollowupGate(db);
        (await gate.HasOpenFollowupAsync(taskId)).Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 3: Upload both documents → all items Uploaded, followup still Open
    //             (workflow is NOT advanced — request maker must explicitly Submit)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BothUploads_AllItemsUploaded_FollowupStillOpen_WorkflowNotAdvanced()
    {
        await using var db = NewDb();
        var requestId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var followup = RaiseFollowup(requestId, taskId, "PassportCopy", "IncomeProof");
        db.DocumentFollowups.Add(followup);
        await db.SaveChangesAsync();

        // First upload — fulfil PassportCopy
        var consumer1 = BuildConsumer(db);
        await consumer1.Consume(BuildContext(requestId, "PassportCopy"));

        // Second upload — fulfil IncomeProof
        var consumer2 = BuildConsumer(db);
        await consumer2.Consume(BuildContext(requestId, "IncomeProof"));

        var persisted = await db.DocumentFollowups
            .AsNoTracking()
            .FirstAsync(f => f.Id == followup.Id);

        // Followup must still be Open — upload alone never resolves it
        persisted.Status.Should().Be(DocumentFollowupStatus.Open,
            "upload alone must NOT resolve the followup; Submit is required");
        persisted.LineItems.Should().OnlyContain(li => li.Status == DocumentFollowupLineItemStatus.Uploaded);

        // Gate still blocks (followup is still Open)
        var gate = new DocumentFollowupGate(db);
        (await gate.HasOpenFollowupAsync(taskId)).Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 4: Raise + Cancel → followup Cancelled, gate unblocked
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RaiseThenCancel_FollowupCancelled_GateUnblocked()
    {
        await using var db = NewDb();
        var taskId = Guid.NewGuid();
        var followup = RaiseFollowup(Guid.NewGuid(), taskId, "PassportCopy", "IncomeProof");
        db.DocumentFollowups.Add(followup);
        await db.SaveChangesAsync();

        // Verify gate was blocking
        var gate = new DocumentFollowupGate(db);
        (await gate.HasOpenFollowupAsync(taskId)).Should().BeTrue();

        // Cancel the followup
        followup.Cancel("No longer required");
        await db.SaveChangesAsync();

        // Gate must now be unblocked
        (await gate.HasOpenFollowupAsync(taskId)).Should().BeFalse();
        followup.Status.Should().Be(DocumentFollowupStatus.Cancelled);
        followup.LineItems.Should().OnlyContain(li => li.Status == DocumentFollowupLineItemStatus.Cancelled);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 5: Upload for non-matching document type → followup unchanged
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_NonMatchingDocumentType_FollowupUnchanged()
    {
        await using var db = NewDb();
        var requestId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var followup = RaiseFollowup(requestId, taskId, "PassportCopy");
        db.DocumentFollowups.Add(followup);
        await db.SaveChangesAsync();

        var consumer = BuildConsumer(db);
        await consumer.Consume(BuildContext(requestId, "BankStatement"));

        var persisted = await db.DocumentFollowups
            .AsNoTracking()
            .FirstAsync(f => f.Id == followup.Id);

        persisted.Status.Should().Be(DocumentFollowupStatus.Open,
            "non-matching upload must not affect followup");
        persisted.LineItems.Should().OnlyContain(li => li.Status == DocumentFollowupLineItemStatus.Pending);
    }
}
