using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Identity;
using Workflow.Data;
using Workflow.DocumentFollowups.Application.Commands;
using Workflow.DocumentFollowups.Domain;
using Workflow.Workflow.Services;
using Xunit;

namespace Workflow.Tests.DocumentFollowups;

public class CancelDocumentFollowupCommandHandlerTests
{
    private static WorkflowDbContext NewDb() =>
        new(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"cancel-{Guid.NewGuid()}").Options);

    private static DocumentFollowup MakeFollowup(string user = "checker-1")
    {
        return DocumentFollowup.Raise(
            Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            "appraisal-initiation-check", user,
            new[] { ("DocA", (string?)null) });
    }

    private static ICurrentUserService UserSubstitute(string username)
    {
        var u = Substitute.For<ICurrentUserService>();
        u.Username.Returns(username);
        u.UserId.Returns((Guid?)null);
        return u;
    }

    [Fact]
    public async Task EmptyReason_Throws()
    {
        await using var db = NewDb();
        var handler = new CancelDocumentFollowupCommandHandler(
            db, Substitute.For<IWorkflowService>(), UserSubstitute("checker-1"),
            Substitute.For<ILogger<CancelDocumentFollowupCommandHandler>>());

        var followup = MakeFollowup();
        db.DocumentFollowups.Add(followup);
        await db.SaveChangesAsync();

        Func<Task> act = () => handler.Handle(new CancelDocumentFollowupCommand(followup.Id, ""), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DifferentUser_Throws()
    {
        await using var db = NewDb();
        var handler = new CancelDocumentFollowupCommandHandler(
            db, Substitute.For<IWorkflowService>(), UserSubstitute("someone-else"),
            Substitute.For<ILogger<CancelDocumentFollowupCommandHandler>>());

        var followup = MakeFollowup("checker-1");
        db.DocumentFollowups.Add(followup);
        await db.SaveChangesAsync();

        Func<Task> act = () => handler.Handle(
            new CancelDocumentFollowupCommand(followup.Id, "withdraw"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RaisingUser_CancelsAndCallsWorkflowService()
    {
        await using var db = NewDb();
        var workflowService = Substitute.For<IWorkflowService>();
        var handler = new CancelDocumentFollowupCommandHandler(
            db, workflowService, UserSubstitute("checker-1"),
            Substitute.For<ILogger<CancelDocumentFollowupCommandHandler>>());

        var followup = MakeFollowup("checker-1");
        followup.AttachFollowupWorkflowInstance(Guid.NewGuid());
        db.DocumentFollowups.Add(followup);
        await db.SaveChangesAsync();

        await handler.Handle(new CancelDocumentFollowupCommand(followup.Id, "withdraw"), CancellationToken.None);

        var loaded = await db.DocumentFollowups.FirstAsync(f => f.Id == followup.Id);
        loaded.Status.Should().Be(DocumentFollowupStatus.Cancelled);
        loaded.CancellationReason.Should().Be("withdraw");

        await workflowService.Received(1).CancelWorkflowAsync(
            Arg.Any<Guid>(), "checker-1", "withdraw", Arg.Any<CancellationToken>());
    }
}
