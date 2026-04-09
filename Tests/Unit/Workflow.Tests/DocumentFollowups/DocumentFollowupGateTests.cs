using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Workflow.Data;
using Workflow.DocumentFollowups.Application;
using Workflow.DocumentFollowups.Domain;
using Xunit;

namespace Workflow.Tests.DocumentFollowups;

public class DocumentFollowupGateTests
{
    private static WorkflowDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"gate-{Guid.NewGuid()}")
            .Options;
        return new WorkflowDbContext(options);
    }

    private static DocumentFollowup MakeFollowup(Guid taskId)
    {
        return DocumentFollowup.Raise(
            appraisalId: Guid.NewGuid(),
            requestId: null,
            raisingWorkflowInstanceId: Guid.NewGuid(),
            raisingPendingTaskId: taskId,
            raisingActivityId: "appraisal-initiation-check",
            raisingUserId: "checker-1",
            lineItems: new[] { ("PassportCopy", (string?)null) });
    }

    [Fact]
    public async Task HasOpenFollowupAsync_NoFollowup_ReturnsFalse()
    {
        await using var db = NewDb();
        var gate = new DocumentFollowupGate(db);

        var result = await gate.HasOpenFollowupAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasOpenFollowupAsync_OpenFollowupExists_ReturnsTrue()
    {
        await using var db = NewDb();
        var taskId = Guid.NewGuid();
        db.DocumentFollowups.Add(MakeFollowup(taskId));
        await db.SaveChangesAsync();

        var gate = new DocumentFollowupGate(db);
        var result = await gate.HasOpenFollowupAsync(taskId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasOpenFollowupAsync_FollowupResolved_ReturnsFalse()
    {
        await using var db = NewDb();
        var taskId = Guid.NewGuid();
        var followup = MakeFollowup(taskId);
        followup.FulfillFirstMatchingByType("PassportCopy", Guid.NewGuid());
        db.DocumentFollowups.Add(followup);
        await db.SaveChangesAsync();

        var gate = new DocumentFollowupGate(db);
        var result = await gate.HasOpenFollowupAsync(taskId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasOpenFollowupAsync_FollowupCancelled_ReturnsFalse()
    {
        await using var db = NewDb();
        var taskId = Guid.NewGuid();
        var followup = MakeFollowup(taskId);
        followup.Cancel("withdrew");
        db.DocumentFollowups.Add(followup);
        await db.SaveChangesAsync();

        var gate = new DocumentFollowupGate(db);
        var result = await gate.HasOpenFollowupAsync(taskId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasOpenFollowupAsync_EmptyTaskId_ReturnsFalse()
    {
        await using var db = NewDb();
        var gate = new DocumentFollowupGate(db);

        var result = await gate.HasOpenFollowupAsync(Guid.Empty);

        result.Should().BeFalse();
    }
}
