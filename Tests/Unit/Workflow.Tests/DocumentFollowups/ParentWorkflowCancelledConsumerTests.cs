using FluentAssertions;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Messaging.Filters;
using Workflow.Data;
using Workflow.DocumentFollowups.Domain;
using Workflow.DocumentFollowups.EventHandlers;
using Workflow.Workflow.Events;
using Workflow.Workflow.Services;
using Xunit;

namespace Workflow.Tests.DocumentFollowups;

public class ParentWorkflowCancelledConsumerTests
{
    private static WorkflowDbContext NewDb() =>
        new(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"cascade-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task CancelsAllOpenFollowupsForParentWorkflow()
    {
        await using var db = NewDb();

        var parentWorkflowId = Guid.NewGuid();
        var f1 = DocumentFollowup.Raise(Guid.NewGuid(), null, parentWorkflowId, Guid.NewGuid(),
            "act", "u1", new[] { ("A", (string?)null) });
        var f2 = DocumentFollowup.Raise(Guid.NewGuid(), null, parentWorkflowId, Guid.NewGuid(),
            "act", "u1", new[] { ("B", (string?)null) });
        var fOther = DocumentFollowup.Raise(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            "act", "u1", new[] { ("C", (string?)null) });

        f1.AttachFollowupWorkflowInstance(Guid.NewGuid());
        f2.AttachFollowupWorkflowInstance(Guid.NewGuid());

        db.DocumentFollowups.AddRange(f1, f2, fOther);
        await db.SaveChangesAsync();

        var workflowService = Substitute.For<IWorkflowService>();
        var inboxGuard = new InboxGuard<WorkflowDbContext>(
            db, Substitute.For<ILogger<InboxGuard<WorkflowDbContext>>>());
        var consumer = new ParentWorkflowCancelledConsumer(
            db, workflowService, Substitute.For<IPublisher>(),
            inboxGuard,
            Substitute.For<ILogger<ParentWorkflowCancelledConsumer>>());

        var ctx = Substitute.For<ConsumeContext<WorkflowCancelled>>();
        ctx.Message.Returns(new WorkflowCancelled
        {
            WorkflowInstanceId = parentWorkflowId,
            CancelledBy = "system",
            CancelledAt = DateTime.UtcNow,
            Reason = "test"
        });
        ctx.CancellationToken.Returns(CancellationToken.None);

        await consumer.Consume(ctx);

        var rows = await db.DocumentFollowups.AsNoTracking().ToListAsync();
        rows.Single(r => r.Id == f1.Id).Status.Should().Be(DocumentFollowupStatus.Cancelled);
        rows.Single(r => r.Id == f2.Id).Status.Should().Be(DocumentFollowupStatus.Cancelled);
        rows.Single(r => r.Id == fOther.Id).Status.Should().Be(DocumentFollowupStatus.Open);

        await workflowService.Received(2).CancelWorkflowAsync(
            Arg.Any<Guid>(), "system", "Parent appraisal cancelled", Arg.Any<CancellationToken>());
    }
}
