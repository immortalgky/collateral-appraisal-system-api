using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Request.Contracts.RequestDocuments;
using Shared.Identity;
using Workflow.Data;
using Workflow.DocumentFollowups.Application.Commands;
using Workflow.DocumentFollowups.Domain;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Services;
using Xunit;

namespace Workflow.Tests.DocumentFollowups;

public class SubmitDocumentFollowupCommandHandlerTests
{
    private static WorkflowDbContext NewDb() =>
        new(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"submit-{Guid.NewGuid()}").Options);

    private static ICurrentUserService User(string name)
    {
        var u = Substitute.For<ICurrentUserService>();
        u.Username.Returns(name);
        u.UserId.Returns((Guid?)null);
        return u;
    }

    private static DocumentFollowup MakeFollowup(params string[] docTypes)
    {
        var lineItems = docTypes.Select(t => (t, (string?)null));
        return DocumentFollowup.Raise(
            appraisalId: Guid.NewGuid(),
            requestId: Guid.NewGuid(),
            raisingWorkflowInstanceId: Guid.NewGuid(),
            raisingPendingTaskId: Guid.NewGuid(),
            raisingActivityId: "appraisal-initiation-check",
            raisingUserId: "checker-1",
            lineItems: lineItems);
    }

    private static async Task<WorkflowInstance> AttachWorkflowAsync(
        WorkflowDbContext db, DocumentFollowup followup, string startedBy)
    {
        var fw = WorkflowInstance.Create(
            workflowDefinitionId: Guid.NewGuid(),
            name: "DocFollowup-test",
            correlationId: followup.Id.ToString(),
            startedBy: startedBy);
        followup.AttachFollowupWorkflowInstance(fw.Id);
        db.DocumentFollowups.Add(followup);
        db.WorkflowInstances.Add(fw);
        await db.SaveChangesAsync();
        return fw;
    }

    private static SubmitDocumentFollowupCommandHandler BuildHandler(
        WorkflowDbContext db,
        IWorkflowService workflowService,
        string actorName,
        IRequestDocumentAttacher? attacher = null) =>
        new(
            db,
            workflowService,
            attacher ?? Substitute.For<IRequestDocumentAttacher>(),
            User(actorName),
            Substitute.For<IPublisher>(),
            Substitute.For<ILogger<SubmitDocumentFollowupCommandHandler>>());

    private static SubmitDocumentFollowupCommand NoAttachments(Guid followupId) =>
        new(followupId, new List<SubmitFollowupAttachmentDto>());

    // ────────────────────────────────────────────────────────────────────────────
    // 1. All Uploaded → Resolved + ResumeWorkflowAsync called with decisionTaken="P"
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_AllUploaded_ResolvesAndResumesWorkflow()
    {
        await using var db = NewDb();
        var followup = MakeFollowup("DocA", "DocB");
        var fw = await AttachWorkflowAsync(db, followup, "requestmaker");

        // Fulfill all items via direct domain method (simulates prior uploads)
        followup.FulfillFirstMatchingByType("DocA", Guid.NewGuid());
        followup.FulfillFirstMatchingByType("DocB", Guid.NewGuid());
        await db.SaveChangesAsync();

        var workflowService = Substitute.For<IWorkflowService>();
        var handler = BuildHandler(db, workflowService, "requestmaker");

        await handler.Handle(NoAttachments(followup.Id), CancellationToken.None);

        var persisted = await db.DocumentFollowups.AsNoTracking().FirstAsync(f => f.Id == followup.Id);
        persisted.Status.Should().Be(DocumentFollowupStatus.Resolved);
        persisted.ResolvedAt.Should().NotBeNull();

        await workflowService.Received(1).ResumeWorkflowAsync(
            fw.Id,
            Arg.Any<string>(),
            "requestmaker",
            Arg.Is<Dictionary<string, object>?>(d =>
                d != null &&
                d.ContainsKey("decisionTaken") &&
                "P".Equals(d["decisionTaken"])),
            Arg.Any<Dictionary<string, RuntimeOverride>?>(),
            Arg.Any<CancellationToken>());
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 2. All Declined → same single path back (decisionTaken="P")
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_AllDeclined_ResolvesAndResumesWithP()
    {
        await using var db = NewDb();
        var followup = MakeFollowup("DocA", "DocB");
        var fw = await AttachWorkflowAsync(db, followup, "requestmaker");

        followup.DeclineLineItem(followup.LineItems[0].Id, "not available");
        followup.DeclineLineItem(followup.LineItems[1].Id, "not available");
        await db.SaveChangesAsync();

        var workflowService = Substitute.For<IWorkflowService>();
        var handler = BuildHandler(db, workflowService, "requestmaker");

        await handler.Handle(NoAttachments(followup.Id), CancellationToken.None);

        var persisted = await db.DocumentFollowups.AsNoTracking().FirstAsync(f => f.Id == followup.Id);
        persisted.Status.Should().Be(DocumentFollowupStatus.Resolved);

        await workflowService.Received(1).ResumeWorkflowAsync(
            fw.Id,
            Arg.Any<string>(),
            "requestmaker",
            Arg.Is<Dictionary<string, object>?>(d =>
                d != null &&
                d.ContainsKey("decisionTaken") &&
                "P".Equals(d["decisionTaken"])),
            Arg.Any<Dictionary<string, RuntimeOverride>?>(),
            Arg.Any<CancellationToken>());
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 3. Mixed Uploaded + Declined → resolves, single path back
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_MixedUploadedAndDeclined_ResolvesAndResumesWithP()
    {
        await using var db = NewDb();
        var followup = MakeFollowup("DocA", "DocB");
        var fw = await AttachWorkflowAsync(db, followup, "requestmaker");

        followup.FulfillFirstMatchingByType("DocA", Guid.NewGuid());
        followup.DeclineLineItem(followup.LineItems[1].Id, "not available");
        await db.SaveChangesAsync();

        var workflowService = Substitute.For<IWorkflowService>();
        var handler = BuildHandler(db, workflowService, "requestmaker");

        await handler.Handle(NoAttachments(followup.Id), CancellationToken.None);

        var persisted = await db.DocumentFollowups.AsNoTracking().FirstAsync(f => f.Id == followup.Id);
        persisted.Status.Should().Be(DocumentFollowupStatus.Resolved);

        await workflowService.Received(1).ResumeWorkflowAsync(
            fw.Id,
            Arg.Any<string>(),
            "requestmaker",
            Arg.Is<Dictionary<string, object>?>(d =>
                d != null &&
                d.ContainsKey("decisionTaken") &&
                "P".Equals(d["decisionTaken"])),
            Arg.Any<Dictionary<string, RuntimeOverride>?>(),
            Arg.Any<CancellationToken>());
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 4. Any Pending item → throws validation error (400-equivalent)
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_WithPendingItem_ThrowsInvalidOperation()
    {
        await using var db = NewDb();
        var followup = MakeFollowup("DocA", "DocB");
        var fw = await AttachWorkflowAsync(db, followup, "requestmaker");

        // Only fulfill one — DocB remains Pending
        followup.FulfillFirstMatchingByType("DocA", Guid.NewGuid());
        await db.SaveChangesAsync();

        var handler = BuildHandler(db, Substitute.For<IWorkflowService>(), "requestmaker");

        Func<Task> act = () => handler.Handle(
            NoAttachments(followup.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Uploaded or Declined*");
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 5. Non-assignee → throws UnauthorizedAccessException
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_ByNonAssignee_ThrowsUnauthorized()
    {
        await using var db = NewDb();
        var followup = MakeFollowup("DocA");
        await AttachWorkflowAsync(db, followup, "requestmaker");

        followup.FulfillFirstMatchingByType("DocA", Guid.NewGuid());
        await db.SaveChangesAsync();

        var handler = BuildHandler(db, Substitute.For<IWorkflowService>(), "someone-else");

        Func<Task> act = () => handler.Handle(
            NoAttachments(followup.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 6. Already Resolved → throws conflict (409-equivalent)
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_OnAlreadyResolved_ThrowsInvalidOperation()
    {
        await using var db = NewDb();
        var followup = MakeFollowup("DocA");
        var fw = await AttachWorkflowAsync(db, followup, "requestmaker");

        followup.FulfillFirstMatchingByType("DocA", Guid.NewGuid());
        await db.SaveChangesAsync();

        // First submit to resolve it
        var workflowService = Substitute.For<IWorkflowService>();
        var handler = BuildHandler(db, workflowService, "requestmaker");
        await handler.Handle(NoAttachments(followup.Id), CancellationToken.None);

        // Second submit on already-resolved followup
        Func<Task> act = () => handler.Handle(
            NoAttachments(followup.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already resolved*");
    }
}
