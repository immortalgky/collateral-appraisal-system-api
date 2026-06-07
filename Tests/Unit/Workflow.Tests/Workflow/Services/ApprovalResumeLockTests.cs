using FluentAssertions;
using NSubstitute;
using Microsoft.Extensions.Logging;
using Shared.Data.Outbox;
using Shared.Exceptions;
using Shared.Time;
using Workflow;
using Workflow.Data.Repository;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Engine;
using Workflow.Workflow.Engine.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Services;
using Xunit;

namespace Workflow.Tests.Workflow.Services;

/// <summary>
/// Verifies the H1 fix: committee-approval resumes acquire a per-instance application lock to
/// serialize concurrent votes, while non-approval resumes do not pay for a lock.
/// </summary>
public class ApprovalResumeLockTests
{
    private readonly IWorkflowEngine _engine = Substitute.For<IWorkflowEngine>();
    private readonly IWorkflowPersistenceService _persistence = Substitute.For<IWorkflowPersistenceService>();
    private readonly IWorkflowEventPublisher _eventPublisher = Substitute.For<IWorkflowEventPublisher>();
    private readonly IWorkflowUnitOfWork _unitOfWork = Substitute.For<IWorkflowUnitOfWork>();
    private readonly IIntegrationEventOutbox _outbox = Substitute.For<IIntegrationEventOutbox>();
    private readonly IAssignmentRepository _assignmentRepository = Substitute.For<IAssignmentRepository>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    private WorkflowService BuildService() =>
        new(_engine, _persistence, _eventPublisher, _unitOfWork, _outbox, _assignmentRepository,
            _clock, Substitute.For<ILogger<WorkflowService>>());

    private (Guid instanceId, string activityId) Arrange(bool isApproval, int lockCode = 0)
    {
        var instanceId = Guid.NewGuid();
        const string activityId = "pending-approval";

        // Run directly in the ambient transaction path (no execution-strategy wrapper).
        _unitOfWork.HasActiveTransaction.Returns(true);

        _persistence.IsInProgressActivityOfTypeAsync(instanceId, activityId,
            ActivityTypes.ApprovalActivity, Arg.Any<CancellationToken>()).Returns(isApproval);
        _persistence.AcquireApplicationLockAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(lockCode);

        var instance = WorkflowInstance.Create(Guid.NewGuid(), "Test Workflow", null, "system");
        _engine.ResumeWorkflowAsync(instanceId, activityId, "voter1",
                Arg.Any<Dictionary<string, object>?>(), Arg.Any<Dictionary<string, RuntimeOverride>?>(),
                Arg.Any<CancellationToken>())
            .Returns(WorkflowExecutionResult.Pending(instance, "next-activity"));

        return (instanceId, activityId);
    }

    [Fact]
    public async Task ApprovalResume_AcquiresPerInstanceLock()
    {
        var (instanceId, activityId) = Arrange(isApproval: true);

        await BuildService().ResumeWorkflowAsync(instanceId, activityId, "voter1");

        await _persistence.Received(1).AcquireApplicationLockAsync(
            $"wf-approval:{instanceId}", "Exclusive", 30000, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NonApprovalResume_DoesNotAcquireLock()
    {
        var (instanceId, activityId) = Arrange(isApproval: false);

        await BuildService().ResumeWorkflowAsync(instanceId, activityId, "voter1");

        await _persistence.DidNotReceive().AcquireApplicationLockAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApprovalResume_LockAcquired_ProceedsToResume()
    {
        // Happy path of a (possibly waiting) second voter: lock granted (code 0) → no throw, resume runs.
        var (instanceId, activityId) = Arrange(isApproval: true, lockCode: 0);

        var act = () => BuildService().ResumeWorkflowAsync(instanceId, activityId, "voter1");

        await act.Should().NotThrowAsync();
        await _engine.Received(1).ResumeWorkflowAsync(instanceId, activityId, "voter1",
            Arg.Any<Dictionary<string, object>?>(), Arg.Any<Dictionary<string, RuntimeOverride>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApprovalResume_LockTimeout_ThrowsConflict()
    {
        var (instanceId, activityId) = Arrange(isApproval: true, lockCode: -1);

        var act = () => BuildService().ResumeWorkflowAsync(instanceId, activityId, "voter1");

        await act.Should().ThrowAsync<ConflictException>();
    }
}
