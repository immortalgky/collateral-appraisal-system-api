using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Workflow.Data.Repository;
using Workflow.DocumentFollowups.Application;
using Workflow.Tasks.Models;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Features.CompleteActivity;
using Workflow.Workflow.Models;
using Workflow.Workflow.Pipeline;
using Workflow.Workflow.Services;
using Xunit;

namespace Workflow.Tests.DocumentFollowups;

/// <summary>
/// Exercises the REAL <see cref="CompleteActivityCommandHandler"/> that Carter dispatches to
/// (from <c>Workflow.Workflow.Features.CompleteActivity</c>) — not the old dead handler in
/// <c>Workflow.Workflow.Commands</c>. Confirms the document followup gate blocks submission
/// when an open followup exists for the current activity's pending task.
/// </summary>
public class CompleteActivityGateTests
{
    private const string JsonWithFollowupFlag = """
    {
      "workflowSchema": {
        "activities": [
          {
            "id": "appraisal-checker",
            "properties": {
              "activityName": "AppraisalChecker",
              "canRaiseFollowup": true
            }
          }
        ]
      }
    }
    """;

    private const string JsonWithoutFollowupFlag = """
    {
      "workflowSchema": {
        "activities": [
          {
            "id": "appraisal-checker",
            "properties": {
              "activityName": "AppraisalChecker"
            }
          }
        ]
      }
    }
    """;

    private static WorkflowInstance BuildInstance(string jsonDefinition)
    {
        var definition = WorkflowDefinition.Create(
            "test-wf", "desc", jsonDefinition, "Test", "tester");

        var instance = WorkflowInstance.Create(
            workflowDefinitionId: definition.Id,
            name: "wf-1",
            correlationId: Guid.NewGuid().ToString(),
            startedBy: "requester");

        // Reflect the private navigation via EF's backing field semantics — private setter,
        // so set through reflection to avoid needing a real DbContext load.
        typeof(WorkflowInstance).GetProperty(nameof(WorkflowInstance.WorkflowDefinition))!
            .SetValue(instance, definition);
        return instance;
    }

    private static CompleteActivityCommandHandler BuildHandler(
        IWorkflowService workflowService,
        IDocumentFollowupGate gate,
        IAssignmentRepository assignmentRepository)
    {
        var pipeline = Substitute.For<IActivityProcessPipeline>();
        pipeline.ExecuteAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(ProcessStepResult.Ok());

        return new CompleteActivityCommandHandler(
            workflowService, pipeline, gate, assignmentRepository);
    }

    [Fact]
    public async Task Rejects_Submission_When_Gate_Has_Open_Followup()
    {
        var instance = BuildInstance(JsonWithFollowupFlag);
        var correlationGuid = Guid.Parse(instance.CorrelationId!);
        var pendingTaskId = Guid.NewGuid();

        var workflowService = Substitute.For<IWorkflowService>();
        workflowService.GetWorkflowInstanceAsync(instance.Id, Arg.Any<CancellationToken>())
            .Returns(instance);

        var assignmentRepository = Substitute.For<IAssignmentRepository>();
        var pendingTask = PendingTask.Create(
            correlationGuid, "AppraisalChecker", "checker-1", "1",
            DateTime.UtcNow, instance.Id, "appraisal-checker");
        pendingTask.Id = pendingTaskId;

        assignmentRepository.GetPendingTaskAsync(correlationGuid, "AppraisalChecker", Arg.Any<CancellationToken>())
            .Returns(pendingTask);

        var gate = Substitute.For<IDocumentFollowupGate>();
        gate.HasOpenFollowupAsync(pendingTaskId, Arg.Any<CancellationToken>()).Returns(true);

        var handler = BuildHandler(workflowService, gate, assignmentRepository);

        var response = await handler.Handle(new CompleteActivityCommand
        {
            WorkflowInstanceId = instance.Id,
            ActivityId = "appraisal-checker",
            CompletedBy = "checker-1",
            Input = new Dictionary<string, object>()
        }, CancellationToken.None);

        response.Status.Should().Be("ValidationFailed");
        response.ValidationErrors.Should().ContainSingle()
            .Which.Should().Contain("open document followups");

        // Critical: ResumeWorkflowAsync must NOT have been called when the gate trips.
        await workflowService.DidNotReceiveWithAnyArgs().ResumeWorkflowAsync(
            default, default!, default!, default, default, default);
    }

    [Fact]
    public async Task Allows_Submission_When_Gate_Is_Open_But_Activity_Not_Opted_In()
    {
        var instance = BuildInstance(JsonWithoutFollowupFlag);

        var workflowService = Substitute.For<IWorkflowService>();
        workflowService.GetWorkflowInstanceAsync(instance.Id, Arg.Any<CancellationToken>())
            .Returns(instance);
        workflowService.ResumeWorkflowAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<Dictionary<string, RuntimeOverride>?>(),
                Arg.Any<CancellationToken>())
            .Returns(instance);

        var assignmentRepository = Substitute.For<IAssignmentRepository>();
        var gate = Substitute.For<IDocumentFollowupGate>();

        var handler = BuildHandler(workflowService, gate, assignmentRepository);

        var response = await handler.Handle(new CompleteActivityCommand
        {
            WorkflowInstanceId = instance.Id,
            ActivityId = "appraisal-checker",
            CompletedBy = "checker-1",
            Input = new Dictionary<string, object>()
        }, CancellationToken.None);

        // Gate must be skipped entirely — no call to HasOpenFollowupAsync, no pending task lookup.
        await gate.DidNotReceiveWithAnyArgs().HasOpenFollowupAsync(default, default);
        await assignmentRepository.DidNotReceiveWithAnyArgs().GetPendingTaskAsync(default, default!, default);
        response.Status.Should().NotBe("ValidationFailed");
    }
}
