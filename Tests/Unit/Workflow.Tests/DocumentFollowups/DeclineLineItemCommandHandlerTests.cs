using FluentAssertions;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Identity;
using Workflow.Data;
using Workflow.DocumentFollowups.Application.Commands;
using Workflow.DocumentFollowups.Domain;
using Workflow.Workflow.Models;
using Xunit;

namespace Workflow.Tests.DocumentFollowups;

public class DeclineLineItemCommandHandlerTests
{
    private static WorkflowDbContext NewDb() =>
        new(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"decline-{Guid.NewGuid()}").Options);

    private static DocumentFollowup MakeFollowup() =>
        DocumentFollowup.Raise(
            Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            "appraisal-initiation-check", "checker-1",
            new[] { ("DocA", (string?)null), ("DocB", (string?)null) });

    private static ICurrentUserService User(string name)
    {
        var u = Substitute.For<ICurrentUserService>();
        u.Username.Returns(name);
        u.UserId.Returns((Guid?)null);
        return u;
    }

    private static async Task<WorkflowInstance> AttachProvisionedWorkflowAsync(
        WorkflowDbContext db, DocumentFollowup followup, string startedBy)
    {
        var fw = WorkflowInstance.Create(
            workflowDefinitionId: Guid.NewGuid(),
            name: "DocFollowup-test",
            correlationId: followup.Id.ToString(),
            startedBy: startedBy);
        followup.AttachFollowupWorkflowInstance(fw.Id);
        db.WorkflowInstances.Add(fw);
        await db.SaveChangesAsync();
        return fw;
    }

    [Fact]
    public async Task EmptyReason_Throws()
    {
        await using var db = NewDb();
        var followup = MakeFollowup();
        db.DocumentFollowups.Add(followup);
        await db.SaveChangesAsync();
        await AttachProvisionedWorkflowAsync(db, followup, "requestmaker");

        var handler = new DeclineLineItemCommandHandler(
            db, User("requestmaker"), Substitute.For<IPublisher>(),
            Substitute.For<IPublishEndpoint>(),
            Substitute.For<ILogger<DeclineLineItemCommandHandler>>());

        Func<Task> act = () => handler.Handle(
            new DeclineLineItemCommand(followup.Id, followup.LineItems[0].Id, " "),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidReason_DeclinesItem()
    {
        await using var db = NewDb();
        var followup = MakeFollowup();
        db.DocumentFollowups.Add(followup);
        await db.SaveChangesAsync();
        await AttachProvisionedWorkflowAsync(db, followup, "requestmaker");

        var handler = new DeclineLineItemCommandHandler(
            db, User("requestmaker"), Substitute.For<IPublisher>(),
            Substitute.For<IPublishEndpoint>(),
            Substitute.For<ILogger<DeclineLineItemCommandHandler>>());

        await handler.Handle(
            new DeclineLineItemCommand(followup.Id, followup.LineItems[0].Id, "not available"),
            CancellationToken.None);

        var reloaded = await db.DocumentFollowups.AsNoTracking().FirstAsync(f => f.Id == followup.Id);
        var item = reloaded.LineItems.First();
        item.Status.Should().Be(DocumentFollowupLineItemStatus.Declined);
        item.Reason.Should().Be("not available");
    }

    [Fact]
    public async Task NotProvisioned_ThrowsInvalidOperation()
    {
        await using var db = NewDb();
        var followup = MakeFollowup();
        db.DocumentFollowups.Add(followup);
        await db.SaveChangesAsync();
        // Intentionally do NOT attach a followup workflow instance — fail-closed path.

        var handler = new DeclineLineItemCommandHandler(
            db, User("requestmaker"), Substitute.For<IPublisher>(),
            Substitute.For<IPublishEndpoint>(),
            Substitute.For<ILogger<DeclineLineItemCommandHandler>>());

        Func<Task> act = () => handler.Handle(
            new DeclineLineItemCommand(followup.Id, followup.LineItems[0].Id, "reason"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not fully provisioned*");
    }

    [Fact]
    public async Task NotAssignee_ThrowsUnauthorized()
    {
        await using var db = NewDb();
        var followup = MakeFollowup();
        db.DocumentFollowups.Add(followup);
        await db.SaveChangesAsync();
        await AttachProvisionedWorkflowAsync(db, followup, "requestmaker");

        var handler = new DeclineLineItemCommandHandler(
            db, User("someone-else"), Substitute.For<IPublisher>(),
            Substitute.For<IPublishEndpoint>(),
            Substitute.For<ILogger<DeclineLineItemCommandHandler>>());

        Func<Task> act = () => handler.Handle(
            new DeclineLineItemCommand(followup.Id, followup.LineItems[0].Id, "reason"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
