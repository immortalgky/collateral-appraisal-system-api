using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;
using Xunit;

namespace Workflow.Tests.Workflow.Services;

public class WorkflowSignalDispatcherTests
{
    private readonly IWorkflowBookmarkRepository _bookmarkRepo;
    private readonly IWorkflowService _workflowService;
    private readonly WorkflowSignalDispatcher _dispatcher;

    public WorkflowSignalDispatcherTests()
    {
        _bookmarkRepo = Substitute.For<IWorkflowBookmarkRepository>();
        _workflowService = Substitute.For<IWorkflowService>();
        _dispatcher = new WorkflowSignalDispatcher(
            _bookmarkRepo,
            _workflowService,
            Substitute.For<ILogger<WorkflowSignalDispatcher>>());
    }

    [Fact]
    public async Task DispatchAsync_MatchingBookmark_ConsumesAndResumes()
    {
        var instanceId = Guid.NewGuid();
        var bookmark = WorkflowBookmark.Create(
            instanceId, "await-appraisal-created",
            BookmarkType.ExternalMessage, "AppraisalCreated",
            correlationId: "req-123");

        _bookmarkRepo.GetBookmarksByCorrelationAsync("req-123", true, Arg.Any<CancellationToken>())
            .Returns(new List<WorkflowBookmark> { bookmark });

        _workflowService.ResumeWorkflowAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Dictionary<string, object>>(), Arg.Any<Dictionary<string, RuntimeOverride>>(),
                Arg.Any<CancellationToken>())
            .Returns(WorkflowInstance.Create(Guid.NewGuid(), "test", null, "system"));

        var payload = new Dictionary<string, object>
        {
            ["appraisalId"] = Guid.NewGuid()
        };

        await _dispatcher.DispatchAsync("AppraisalCreated", "req-123", payload);

        bookmark.IsConsumed.Should().BeTrue();
        bookmark.ConsumedBy.Should().Be("SYSTEM");

        await _workflowService.Received(1).ResumeWorkflowAsync(
            instanceId, "await-appraisal-created", "SYSTEM",
            Arg.Is<Dictionary<string, object>>(d =>
                d.ContainsKey("appraisalId") &&
                d["completedBy"].Equals("SYSTEM") &&
                d["signalName"].Equals("AppraisalCreated")),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_NoMatchingBookmarks_DoesNotResume()
    {
        _bookmarkRepo.GetBookmarksByCorrelationAsync("req-999", true, Arg.Any<CancellationToken>())
            .Returns(new List<WorkflowBookmark>());

        await _dispatcher.DispatchAsync("AppraisalCreated", "req-999",
            new Dictionary<string, object>());

        await _workflowService.DidNotReceive().ResumeWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<Dictionary<string, RuntimeOverride>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_BookmarkWrongSignalName_DoesNotResume()
    {
        var bookmark = WorkflowBookmark.Create(
            Guid.NewGuid(), "some-activity",
            BookmarkType.ExternalMessage, "DifferentSignal",
            correlationId: "req-123");

        _bookmarkRepo.GetBookmarksByCorrelationAsync("req-123", true, Arg.Any<CancellationToken>())
            .Returns(new List<WorkflowBookmark> { bookmark });

        await _dispatcher.DispatchAsync("AppraisalCreated", "req-123",
            new Dictionary<string, object>());

        await _workflowService.DidNotReceive().ResumeWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<Dictionary<string, RuntimeOverride>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_BookmarkWrongType_DoesNotResume()
    {
        var bookmark = WorkflowBookmark.Create(
            Guid.NewGuid(), "some-activity",
            BookmarkType.Timer, "AppraisalCreated",
            correlationId: "req-123");

        _bookmarkRepo.GetBookmarksByCorrelationAsync("req-123", true, Arg.Any<CancellationToken>())
            .Returns(new List<WorkflowBookmark> { bookmark });

        await _dispatcher.DispatchAsync("AppraisalCreated", "req-123",
            new Dictionary<string, object>());

        await _workflowService.DidNotReceive().ResumeWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<Dictionary<string, RuntimeOverride>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_AlreadyConsumedBookmark_SkipsWithoutError()
    {
        var bookmark = WorkflowBookmark.Create(
            Guid.NewGuid(), "await-appraisal-created",
            BookmarkType.ExternalMessage, "AppraisalCreated",
            correlationId: "req-123");

        // Pre-consume the bookmark
        bookmark.Consume("previous-consumer");

        _bookmarkRepo.GetBookmarksByCorrelationAsync("req-123", true, Arg.Any<CancellationToken>())
            .Returns(new List<WorkflowBookmark> { bookmark });

        await _dispatcher.DispatchAsync("AppraisalCreated", "req-123",
            new Dictionary<string, object>());

        await _workflowService.DidNotReceive().ResumeWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<Dictionary<string, RuntimeOverride>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_MultipleBookmarks_ResumesAll()
    {
        var instanceId1 = Guid.NewGuid();
        var instanceId2 = Guid.NewGuid();

        var bookmark1 = WorkflowBookmark.Create(
            instanceId1, "await-node",
            BookmarkType.ExternalMessage, "AppraisalCreated",
            correlationId: "req-123");
        var bookmark2 = WorkflowBookmark.Create(
            instanceId2, "await-node",
            BookmarkType.ExternalMessage, "AppraisalCreated",
            correlationId: "req-123");

        _bookmarkRepo.GetBookmarksByCorrelationAsync("req-123", true, Arg.Any<CancellationToken>())
            .Returns(new List<WorkflowBookmark> { bookmark1, bookmark2 });

        _workflowService.ResumeWorkflowAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Dictionary<string, object>>(), Arg.Any<Dictionary<string, RuntimeOverride>>(),
                Arg.Any<CancellationToken>())
            .Returns(WorkflowInstance.Create(Guid.NewGuid(), "test", null, "system"));

        await _dispatcher.DispatchAsync("AppraisalCreated", "req-123",
            new Dictionary<string, object> { ["appraisalId"] = Guid.NewGuid() });

        bookmark1.IsConsumed.Should().BeTrue();
        bookmark2.IsConsumed.Should().BeTrue();

        await _workflowService.Received(2).ResumeWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<Dictionary<string, RuntimeOverride>>(),
            Arg.Any<CancellationToken>());
    }
}
