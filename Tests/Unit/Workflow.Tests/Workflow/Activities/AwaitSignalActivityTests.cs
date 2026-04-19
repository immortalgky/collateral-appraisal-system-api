using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Time;
using Workflow.Data;
using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using Xunit;

namespace Workflow.Tests.Workflow.Activities;

public class AwaitSignalActivityTests
{
    private readonly WorkflowDbContext _dbContext;
    private readonly AwaitSignalActivity _activity;

    public AwaitSignalActivityTests()
    {
        _dbContext = new WorkflowDbContext(
            new DbContextOptionsBuilder<WorkflowDbContext>()
                .UseInMemoryDatabase($"await-signal-{Guid.NewGuid()}")
                .Options);
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(new DateTime(2026, 4, 19, 12, 0, 0));
        dateTimeProvider.Now.Returns(new DateTime(2026, 4, 19, 12, 0, 0));
        _activity = new AwaitSignalActivity(
            _dbContext,
            dateTimeProvider,
            Substitute.For<ILogger<AwaitSignalActivity>>());
    }

    [Fact]
    public void ActivityProperties_ShouldReturnCorrectValues()
    {
        _activity.ActivityType.Should().Be(ActivityTypes.AwaitSignalActivity);
        _activity.Name.Should().Be("Await Signal Activity");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidConfig_ReturnsPendingAndCreatesBookmark()
    {
        var requestId = Guid.NewGuid().ToString();
        var context = CreateContext(
            properties: new Dictionary<string, object>
            {
                ["signalName"] = "AppraisalCreated",
                ["correlationKey"] = "requestId",
                ["completionVariable"] = "appraisalId"
            },
            variables: new Dictionary<string, object>
            {
                ["requestId"] = requestId
            });

        var result = await _activity.ExecuteAsync(context);

        result.Status.Should().Be(ActivityResultStatus.Pending);
        result.OutputData["signalName"].Should().Be("AppraisalCreated");
        result.OutputData["correlationValue"].Should().Be(requestId);

        // Verify bookmark was added to the change tracker (engine's outer
        // SaveChangesAsync flushes this in production)
        var allTracked = _dbContext.ChangeTracker.Entries()
            .Select(e => $"{e.Entity.GetType().Name}:{e.State}")
            .ToList();

        var trackedBookmarks = _dbContext.ChangeTracker.Entries<WorkflowBookmark>()
            .Select(e => e.Entity)
            .ToList();

        trackedBookmarks.Should().HaveCount(1, "tracked entities: [{0}]", string.Join(", ", allTracked));
        trackedBookmarks[0].Key.Should().Be("AppraisalCreated");
        trackedBookmarks[0].CorrelationId.Should().Be(requestId);
        trackedBookmarks[0].Type.Should().Be(BookmarkType.ExternalMessage);
        trackedBookmarks[0].IsConsumed.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_CompletionVariableAlreadySet_SkipsSuspend()
    {
        var context = CreateContext(
            properties: new Dictionary<string, object>
            {
                ["signalName"] = "AppraisalCreated",
                ["correlationKey"] = "requestId",
                ["completionVariable"] = "appraisalId"
            },
            variables: new Dictionary<string, object>
            {
                ["requestId"] = Guid.NewGuid().ToString(),
                ["appraisalId"] = Guid.NewGuid().ToString()
            });

        var result = await _activity.ExecuteAsync(context);

        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData["signalSkipped"].Should().Be(true);

        var trackedBookmarks = _dbContext.ChangeTracker.Entries<WorkflowBookmark>()
            .Where(e => e.State == EntityState.Added)
            .ToList();
        trackedBookmarks.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_MissingSignalName_ReturnsFailed()
    {
        var context = CreateContext(
            properties: new Dictionary<string, object>
            {
                ["correlationKey"] = "requestId"
            },
            variables: new Dictionary<string, object>
            {
                ["requestId"] = Guid.NewGuid().ToString()
            });

        var result = await _activity.ExecuteAsync(context);

        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("signalName");
    }

    [Fact]
    public async Task ExecuteAsync_MissingCorrelationVariable_ReturnsFailed()
    {
        var context = CreateContext(
            properties: new Dictionary<string, object>
            {
                ["signalName"] = "AppraisalCreated",
                ["correlationKey"] = "requestId"
            },
            variables: new Dictionary<string, object>());

        var result = await _activity.ExecuteAsync(context);

        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("requestId");
    }

    [Fact]
    public async Task ResumeAsync_MergesPayloadIntoOutput()
    {
        var appraisalId = Guid.NewGuid();
        var context = CreateContext(
            properties: new Dictionary<string, object>
            {
                ["signalName"] = "AppraisalCreated",
                ["correlationKey"] = "requestId"
            },
            variables: new Dictionary<string, object>
            {
                ["requestId"] = Guid.NewGuid().ToString()
            });

        // First execute to create the execution record
        await _activity.ExecuteAsync(context);

        var resumeInput = new Dictionary<string, object>
        {
            ["appraisalId"] = appraisalId,
            ["appraisalNumber"] = "APR-001",
            ["completedBy"] = "SYSTEM",
            ["signalName"] = "AppraisalCreated"
        };

        var result = await _activity.ResumeAsync(context, resumeInput);

        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData["appraisalId"].Should().Be(appraisalId);
        result.OutputData["appraisalNumber"].Should().Be("APR-001");
    }

    private static ActivityContext CreateContext(
        Dictionary<string, object> properties,
        Dictionary<string, object> variables)
    {
        var workflowInstance = WorkflowInstance.Create(
            Guid.NewGuid(), "Test Workflow", Guid.NewGuid().ToString(), "system");

        return new ActivityContext
        {
            ActivityId = "await-appraisal-created",
            ActivityName = "Await Appraisal Created",
            WorkflowInstance = workflowInstance,
            WorkflowInstanceId = workflowInstance.Id,
            Variables = variables,
            Properties = properties
        };
    }
}
