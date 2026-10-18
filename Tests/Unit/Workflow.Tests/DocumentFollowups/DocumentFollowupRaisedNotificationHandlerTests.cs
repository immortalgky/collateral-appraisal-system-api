using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Parameter.Contracts.DocumentRequirements;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Workflow.Data;
using Workflow.DocumentFollowups.Domain;
using Workflow.DocumentFollowups.Domain.Events;
using Workflow.DocumentFollowups.EventHandlers;
using Workflow.Workflow.Models;
using Xunit;

namespace Workflow.Tests.DocumentFollowups;

public class DocumentFollowupRaisedNotificationHandlerTests
{
    [Fact]
    public async Task Handle_PublishesRequiredEventWithDocumentsInOrder()
    {
        await using var db = new WorkflowDbContext(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"raised-{Guid.NewGuid()}").Options);
        var parent = WorkflowInstance.Create(Guid.NewGuid(), "wf", null, "checker-1");
        db.WorkflowInstances.Add(parent);
        await db.SaveChangesAsync();

        var checklist = Substitute.For<IDocumentChecklistService>();
        checklist.GetAllDocumentTypeNamesAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string> { ["D001"] = "Complete Valuation Report" });
        var outbox = Substitute.For<IIntegrationEventOutbox>();
        var handler = new DocumentFollowupRaisedNotificationHandler(
            db, outbox, checklist, Substitute.For<ILogger<DocumentFollowupRaisedNotificationHandler>>());

        var followup = DocumentFollowup.Raise(
            Guid.NewGuid(), null, parent.Id, Guid.NewGuid(), "appraisal-initiation-check", "checker-1",
            new[] { ("d001", (string?)"please attach"), ("UNKNOWN", (string?)null) });
        var raised = followup.DomainEvents.OfType<DocumentFollowupRaisedDomainEvent>().Single();

        await handler.Handle(raised, CancellationToken.None);

        var published = outbox.ReceivedCalls()
            .Select(c => c.GetArguments()[0])
            .OfType<DocumentFollowupRequiredIntegrationEvent>()
            .Single();
        published.Reason.Should().Be("Missing documents: Complete Valuation Report, UNKNOWN");
        published.Documents.Should().Equal(
            new DocumentFollowupRequiredDocument("d001", "Complete Valuation Report", "please attach"),
            new DocumentFollowupRequiredDocument("UNKNOWN", "UNKNOWN", ""));
    }
}
