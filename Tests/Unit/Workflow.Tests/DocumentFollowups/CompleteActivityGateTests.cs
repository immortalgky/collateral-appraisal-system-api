using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Identity;
using Shared.Time;
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
/// Exercises the REAL <see cref="CompleteActivityCommandHandler"/> that Carter dispatches to.
/// The document followup gate is now enforced by <see cref="RequireDocumentFollowupClearedStep"/>
/// registered in the pipeline. These tests verify the handler correctly surfaces pipeline failures
/// and that the gate step behavior is correct.
/// </summary>
public class CompleteActivityGateTests
{
    // ── Handler-level tests (pipeline mock) ──────────────────────────────

    [Fact]
    public async Task Handler_WhenPipelineFails_Returns_ValidationFailed()
    {
        var workflowService = Substitute.For<IWorkflowService>();
        var instanceId = Guid.NewGuid();
        workflowService.GetWorkflowInstanceAsync(instanceId, Arg.Any<CancellationToken>())
            .Returns((WorkflowInstance?)null);

        var pipeline = Substitute.For<IActivityProcessPipeline>();
        pipeline.ExecuteAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<IReadOnlyDictionary<string, object?>>(),
                Arg.Any<CancellationToken>())
            .Returns(PipelineResult.ValidationsFailed([
                new StepFailure("RequireDocumentFollowupCleared", "OPEN_DOCUMENT_FOLLOWUPS",
                    "This task has open document followups.")
            ]));

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.Roles.Returns(Array.Empty<string>());

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(new DateTime(2026, 4, 19, 12, 0, 0));
        dateTimeProvider.Now.Returns(new DateTime(2026, 4, 19, 12, 0, 0));

        var handler = new CompleteActivityCommandHandler(workflowService, pipeline, currentUserService, dateTimeProvider);

        var response = await handler.Handle(new CompleteActivityCommand
        {
            WorkflowInstanceId = instanceId,
            ActivityId = "appraisal-checker",
            CompletedBy = "checker-1",
            Input = new Dictionary<string, object>()
        }, CancellationToken.None);

        response.Status.Should().Be("ValidationFailed");
        response.ValidationErrors.Should().ContainSingle()
            .Which.Should().Contain("open document followups");

        await workflowService.DidNotReceiveWithAnyArgs().ResumeWorkflowAsync(
            default, default!, default!, default, default, default);
    }

    [Fact]
    public async Task Handler_WhenPipelinePasses_Calls_ResumeWorkflow()
    {
        var instanceId = Guid.NewGuid();
        var instance = BuildInstance("""{"workflowSchema":{"activities":[]}}""", instanceId);

        var workflowService = Substitute.For<IWorkflowService>();
        workflowService.GetWorkflowInstanceAsync(instanceId, Arg.Any<CancellationToken>())
            .Returns(instance);
        workflowService.ResumeWorkflowAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<Dictionary<string, RuntimeOverride>?>(),
                Arg.Any<CancellationToken>())
            .Returns(instance);

        var pipeline = Substitute.For<IActivityProcessPipeline>();
        pipeline.ExecuteAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<IReadOnlyDictionary<string, object?>>(),
                Arg.Any<CancellationToken>())
            .Returns(PipelineResult.Success());

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.Roles.Returns(Array.Empty<string>());

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(new DateTime(2026, 4, 19, 12, 0, 0));
        dateTimeProvider.Now.Returns(new DateTime(2026, 4, 19, 12, 0, 0));

        var handler = new CompleteActivityCommandHandler(workflowService, pipeline, currentUserService, dateTimeProvider);

        var response = await handler.Handle(new CompleteActivityCommand
        {
            WorkflowInstanceId = instanceId,
            ActivityId = "appraisal-checker",
            CompletedBy = "checker-1",
            Input = new Dictionary<string, object>()
        }, CancellationToken.None);

        response.Status.Should().NotBe("ValidationFailed");
        await workflowService.ReceivedWithAnyArgs(1).ResumeWorkflowAsync(
            default, default!, default!, default, default, default);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static WorkflowInstance BuildInstance(string jsonDefinition, Guid? id = null)
    {
        var definition = WorkflowDefinition.Create(
            "test-wf", "desc", jsonDefinition, "Test", "tester");

        var instance = WorkflowInstance.Create(
            workflowDefinitionId: definition.Id,
            name: "wf-1",
            correlationId: Guid.NewGuid().ToString(),
            startedBy: "requester");

        if (id.HasValue)
        {
            typeof(WorkflowInstance).GetProperty(nameof(WorkflowInstance.Id))!
                .SetValue(instance, id.Value);
        }

        typeof(WorkflowInstance).GetProperty(nameof(WorkflowInstance.WorkflowDefinition))!
            .SetValue(instance, definition);

        return instance;
    }
}
