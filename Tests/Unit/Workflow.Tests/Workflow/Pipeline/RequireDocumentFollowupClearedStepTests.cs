using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Workflow.Data.Repository;
using Workflow.DocumentFollowups.Application;
using Workflow.Tasks.Models;
using Workflow.Workflow.Models;
using Workflow.Workflow.Pipeline;
using Workflow.Workflow.Pipeline.Steps;
using Workflow.Workflow.Repositories;
using Xunit;

namespace Workflow.Tests.Workflow.Pipeline;

/// <summary>
/// Golden-master tests for RequireDocumentFollowupClearedStep.
/// Validates parity with the legacy inline gate that was previously embedded in
/// CompleteActivityCommandHandler. Same inputs must produce same Pass/Fail outcomes.
/// </summary>
public class RequireDocumentFollowupClearedStepTests
{
    // ── Infrastructure ────────────────────────────────────────────────────

    private const string ActivityId = "appraisal-checker";

    private static readonly string JsonDefinitionWithFollowup = $$"""
        {
          "workflowSchema": {
            "activities": [
              {
                "id": "{{ActivityId}}",
                "type": "TaskActivity",
                "properties": {
                  "activityName": "Appraisal Checker",
                  "canRaiseFollowup": true
                }
              }
            ]
          }
        }
        """;

    private static readonly string JsonDefinitionWithoutFollowup = $$"""
        {
          "workflowSchema": {
            "activities": [
              {
                "id": "{{ActivityId}}",
                "type": "TaskActivity",
                "properties": {
                  "activityName": "Appraisal Checker"
                }
              }
            ]
          }
        }
        """;

    private static WorkflowInstance BuildInstance(string jsonDefinition)
    {
        var definition = WorkflowDefinition.Create("test-wf", "desc", jsonDefinition, "Test", "tester");
        var instance = WorkflowInstance.Create(definition.Id, "wf-1", null, "tester");
        typeof(WorkflowInstance)
            .GetProperty(nameof(WorkflowInstance.WorkflowDefinition))!
            .SetValue(instance, definition);
        return instance;
    }

    private static PendingTask BuildPendingTask(Guid taskId, Guid correlationId)
    {
        var task = PendingTask.Create(
            correlationId, "Appraisal Checker", "john.doe", "1",
            DateTime.UtcNow, Guid.NewGuid(), ActivityId);
        task.Id = taskId;
        return task;
    }

    private static ProcessStepContext BuildCtx(Guid workflowInstanceId) => new()
    {
        WorkflowInstanceId = workflowInstanceId,
        ActivityId = ActivityId,
        ActivityName = ActivityId,
        CompletedBy = "john.doe",
        UserRoles = [],
        Variables = new Dictionary<string, object?>(),
        Input = new Dictionary<string, object?>()
    };

    private static RequireDocumentFollowupClearedStep BuildSut(
        IWorkflowInstanceRepository instanceRepo,
        IAssignmentRepository assignmentRepo,
        IDocumentFollowupGate gate)
    {
        return new RequireDocumentFollowupClearedStep(
            instanceRepo,
            assignmentRepo,
            gate,
            Substitute.For<ILogger<RequireDocumentFollowupClearedStep>>());
    }

    // ── Matrix tests (golden-master) ──────────────────────────────────────

    // GM-1: Activity opted in, no open followups → Pass
    [Fact]
    public async Task ActivityOptedIn_NoOpenFollowups_ReturnsPass()
    {
        var workflowInstanceId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var correlationId = workflowInstanceId;

        var instance = BuildInstance(JsonDefinitionWithFollowup);
        instance.Id = workflowInstanceId;

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(instance);

        var assignmentRepo = Substitute.For<IAssignmentRepository>();
        assignmentRepo.GetPendingTaskAsync(correlationId, "Appraisal Checker", Arg.Any<CancellationToken>())
            .Returns(BuildPendingTask(taskId, correlationId));

        var gate = Substitute.For<IDocumentFollowupGate>();
        gate.HasOpenFollowupAsync(taskId, Arg.Any<CancellationToken>())
            .Returns(false);

        var sut = BuildSut(instanceRepo, assignmentRepo, gate);

        var result = await sut.ExecuteAsync(BuildCtx(workflowInstanceId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("no open followups should allow completion");
    }

    // GM-2: Activity opted in, one open followup → Fail
    [Fact]
    public async Task ActivityOptedIn_OneOpenFollowup_ReturnsFail()
    {
        var workflowInstanceId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var correlationId = workflowInstanceId;

        var instance = BuildInstance(JsonDefinitionWithFollowup);
        instance.Id = workflowInstanceId;

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(instance);

        var assignmentRepo = Substitute.For<IAssignmentRepository>();
        assignmentRepo.GetPendingTaskAsync(correlationId, "Appraisal Checker", Arg.Any<CancellationToken>())
            .Returns(BuildPendingTask(taskId, correlationId));

        var gate = Substitute.For<IDocumentFollowupGate>();
        gate.HasOpenFollowupAsync(taskId, Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = BuildSut(instanceRepo, assignmentRepo, gate);

        var result = await sut.ExecuteAsync(BuildCtx(workflowInstanceId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse("an open followup should block completion");
        var failed = result.Should().BeOfType<ProcessStepResult.Failed>().Which;
        failed.ErrorCode.Should().Be("OPEN_DOCUMENT_FOLLOWUPS");
    }

    // GM-3: Activity NOT opted in (no canRaiseFollowup flag) → Pass regardless
    [Fact]
    public async Task ActivityNotOptedIn_NoFollowupCheck_ReturnsPass()
    {
        var workflowInstanceId = Guid.NewGuid();

        var instance = BuildInstance(JsonDefinitionWithoutFollowup);
        instance.Id = workflowInstanceId;

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(instance);

        var assignmentRepo = Substitute.For<IAssignmentRepository>();
        var gate = Substitute.For<IDocumentFollowupGate>();

        var sut = BuildSut(instanceRepo, assignmentRepo, gate);

        var result = await sut.ExecuteAsync(BuildCtx(workflowInstanceId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("gate does not apply when canRaiseFollowup is not set");
        await gate.DidNotReceiveWithAnyArgs().HasOpenFollowupAsync(default, default);
    }

    // GM-4: Activity opted in, no pending task found → Pass (gate does not apply)
    [Fact]
    public async Task ActivityOptedIn_NoPendingTask_ReturnsPass()
    {
        var workflowInstanceId = Guid.NewGuid();
        var correlationId = workflowInstanceId;

        var instance = BuildInstance(JsonDefinitionWithFollowup);
        instance.Id = workflowInstanceId;

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(instance);

        var assignmentRepo = Substitute.For<IAssignmentRepository>();
        assignmentRepo.GetPendingTaskAsync(correlationId, "Appraisal Checker", Arg.Any<CancellationToken>())
            .Returns((PendingTask?)null);

        var gate = Substitute.For<IDocumentFollowupGate>();

        var sut = BuildSut(instanceRepo, assignmentRepo, gate);

        var result = await sut.ExecuteAsync(BuildCtx(workflowInstanceId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("missing pending task means gate does not apply");
        await gate.DidNotReceiveWithAnyArgs().HasOpenFollowupAsync(default, default);
    }

    // GM-5: Workflow instance not found → Pass (cannot block)
    [Fact]
    public async Task WorkflowInstanceNotFound_ReturnsPass()
    {
        var workflowInstanceId = Guid.NewGuid();

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns((WorkflowInstance?)null);

        var assignmentRepo = Substitute.For<IAssignmentRepository>();
        var gate = Substitute.For<IDocumentFollowupGate>();

        var sut = BuildSut(instanceRepo, assignmentRepo, gate);

        var result = await sut.ExecuteAsync(BuildCtx(workflowInstanceId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("missing instance should not cause a hard failure");
    }
}
