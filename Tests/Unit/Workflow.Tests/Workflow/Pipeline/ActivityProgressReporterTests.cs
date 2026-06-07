using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Workflow.Data;
using Workflow.Data.Entities;
using Workflow.Workflow.Pipeline;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Models;
using Xunit;

namespace Workflow.Tests.Workflow.Pipeline;

/// <summary>
/// Verifies that <see cref="IActivityProgressReporter"/> is called at the correct
/// lifecycle boundaries and that a throwing reporter never bubbles up into the
/// pipeline result.
/// </summary>
public class ActivityProgressReporterTests
{
    // ── Infrastructure ────────────────────────────────────────────────────

    private static WorkflowDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"reporter-{Guid.NewGuid()}")
            .Options;
        return new WorkflowDbContext(options);
    }

    private static IActivityProcessStep MakeStep(string name, StepKind kind, ProcessStepResult result)
    {
        var step = Substitute.For<IActivityProcessStep>();
        step.Descriptor.Returns(StepDescriptor.For<object>(name, $"{name} Display", kind));
        step.ExecuteAsync(Arg.Any<ProcessStepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));
        return step;
    }

    private static ActivityProcessConfiguration MakeConfig(
        string activityName, string processorName, StepKind kind, int sortOrder)
    {
        return ActivityProcessConfiguration.Create(
            activityName, processorName + " label", processorName, kind, sortOrder, "system");
    }

    private static WorkflowInstance MakeWorkflowInstance(Guid id)
    {
        var instance = WorkflowInstance.Create(Guid.NewGuid(), "test-wf", null, "tester");
        instance.Id = id;
        return instance;
    }

    private static IActivityProgressReporter MakeReporter()
    {
        var reporter = Substitute.For<IActivityProgressReporter>();
        reporter.PipelineStarted(default, default!, default!, default!, default)
            .ReturnsForAnyArgs(Task.CompletedTask);
        reporter.StepStarted(default, default!, default!, default)
            .ReturnsForAnyArgs(Task.CompletedTask);
        reporter.StepFinished(default, default!, default!, default, default!, default)
            .ReturnsForAnyArgs(Task.CompletedTask);
        reporter.PipelineFinished(default, default!, default!, default)
            .ReturnsForAnyArgs(Task.CompletedTask);
        return reporter;
    }

    private static ActivityProcessPipeline BuildPipeline(
        WorkflowDbContext db,
        IWorkflowInstanceRepository instanceRepo,
        IEnumerable<IActivityProcessStep> steps,
        IActivityProgressReporter reporter)
    {
        var predicateEvaluator = Substitute.For<IPredicateEvaluator>();
        var sink = Substitute.For<IActivityProcessExecutionSink>();
        sink.PersistAsync(Arg.Any<IReadOnlyList<ActivityProcessExecution>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return new ActivityProcessPipeline(
            db,
            instanceRepo,
            predicateEvaluator,
            steps,
            sink,
            reporter,
            Substitute.For<ILogger<ActivityProcessPipeline>>());
    }

    private static IReadOnlyList<string> NoRoles => Array.Empty<string>();
    private static IReadOnlyDictionary<string, object?> NoInput => new Dictionary<string, object?>();
    private static IReadOnlyCollection<string> NoAck => Array.Empty<string>();

    // ── Tests ─────────────────────────────────────────────────────────────

    // R1: PipelineStarted is called once, StepStarted/StepFinished once per step, PipelineFinished once
    [Fact]
    public async Task ReporterCalls_HappyPath_OneValidationOneAction()
    {
        await using var db = NewDb();
        var workflowInstanceId = Guid.NewGuid();
        var activityName = "test-activity";
        var completedBy = "user1";

        var v1 = MakeStep("ValA", StepKind.Validation, ProcessStepResult.Pass());
        var a1 = MakeStep("ActA", StepKind.Action, ProcessStepResult.Pass());

        db.ActivityProcessConfigurations.AddRange(
            MakeConfig(activityName, "ValA", StepKind.Validation, 1),
            MakeConfig(activityName, "ActA", StepKind.Action, 1));
        await db.SaveChangesAsync();

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(MakeWorkflowInstance(workflowInstanceId));

        var reporter = MakeReporter();
        var pipeline = BuildPipeline(db, instanceRepo, [v1, a1], reporter);
        var execId = Guid.NewGuid();

        var result = await pipeline.ExecuteAsync(
            workflowInstanceId, execId, activityName, completedBy, NoRoles, NoInput, NoAck, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // PipelineStarted called once with both steps in the list
        await reporter.Received(1).PipelineStarted(
            execId,
            activityName,
            Arg.Is<IReadOnlyList<StepInfo>>(s => s.Count == 2),
            completedBy,
            Arg.Any<CancellationToken>());

        // StepStarted and StepFinished once each for ValA and ActA
        await reporter.Received(2).StepStarted(execId, Arg.Any<StepInfo>(), completedBy, Arg.Any<CancellationToken>());
        await reporter.Received(2).StepFinished(execId, Arg.Any<StepInfo>(), Arg.Any<string>(), Arg.Any<int>(), completedBy, Arg.Any<CancellationToken>());

        // PipelineFinished called once
        await reporter.Received(1).PipelineFinished(execId, "Success", completedBy, Arg.Any<CancellationToken>());
    }

    // R2: ValidationsFailed → PipelineFinished("ValidationsFailed")
    [Fact]
    public async Task ReporterCalls_ValidationsFailed_FinishedWithValidationsFailed()
    {
        await using var db = NewDb();
        var workflowInstanceId = Guid.NewGuid();
        var activityName = "test-activity";
        var completedBy = "user1";

        var v1 = MakeStep("ValA", StepKind.Validation, ProcessStepResult.Fail("ERR", "fail"));

        db.ActivityProcessConfigurations.Add(
            MakeConfig(activityName, "ValA", StepKind.Validation, 1));
        await db.SaveChangesAsync();

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(MakeWorkflowInstance(workflowInstanceId));

        var reporter = MakeReporter();
        var pipeline = BuildPipeline(db, instanceRepo, [v1], reporter);
        var execId = Guid.NewGuid();

        var result = await pipeline.ExecuteAsync(
            workflowInstanceId, execId, activityName, completedBy, NoRoles, NoInput, NoAck, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        await reporter.Received(1).PipelineFinished(execId, "ValidationsFailed", completedBy, Arg.Any<CancellationToken>());
    }

    // R3: ActionFailed → PipelineFinished("ActionFailed")
    [Fact]
    public async Task ReporterCalls_ActionFailed_FinishedWithActionFailed()
    {
        await using var db = NewDb();
        var workflowInstanceId = Guid.NewGuid();
        var activityName = "test-activity";
        var completedBy = "user1";

        var a1 = MakeStep("ActA", StepKind.Action, ProcessStepResult.Fail("ACT_ERR", "action fail"));

        db.ActivityProcessConfigurations.Add(
            MakeConfig(activityName, "ActA", StepKind.Action, 1));
        await db.SaveChangesAsync();

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(MakeWorkflowInstance(workflowInstanceId));

        var reporter = MakeReporter();
        var pipeline = BuildPipeline(db, instanceRepo, [a1], reporter);
        var execId = Guid.NewGuid();

        var result = await pipeline.ExecuteAsync(
            workflowInstanceId, execId, activityName, completedBy, NoRoles, NoInput, NoAck, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        await reporter.Received(1).PipelineFinished(execId, "ActionFailed", completedBy, Arg.Any<CancellationToken>());
    }

    // R4: A throwing reporter is swallowed — pipeline result is unaffected
    [Fact]
    public async Task ReporterThrows_IsSwallowed_PipelineResultUnaffected()
    {
        await using var db = NewDb();
        var workflowInstanceId = Guid.NewGuid();
        var activityName = "test-activity";

        var v1 = MakeStep("ValA", StepKind.Validation, ProcessStepResult.Pass());

        db.ActivityProcessConfigurations.Add(
            MakeConfig(activityName, "ValA", StepKind.Validation, 1));
        await db.SaveChangesAsync();

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(MakeWorkflowInstance(workflowInstanceId));

        // Make the reporter throw on every call
        var reporter = Substitute.For<IActivityProgressReporter>();
        reporter.PipelineStarted(default, default!, default!, default!, default)
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("SignalR unavailable"));
        reporter.StepStarted(default, default!, default!, default)
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("SignalR unavailable"));
        reporter.StepFinished(default, default!, default!, default, default!, default)
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("SignalR unavailable"));
        reporter.PipelineFinished(default, default!, default!, default)
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("SignalR unavailable"));

        var pipeline = BuildPipeline(db, instanceRepo, [v1], reporter);

        // Should not throw despite the reporter throwing
        var result = await pipeline.ExecuteAsync(
            workflowInstanceId, Guid.NewGuid(), activityName, "user", NoRoles, NoInput, NoAck, CancellationToken.None);

        result.IsSuccess.Should().BeTrue("a throwing reporter must never affect the pipeline result");
    }

    // R5: StepInfo DisplayName is resolved from descriptor when step is registered
    [Fact]
    public async Task StepInfo_DisplayName_ResolvedFromDescriptor()
    {
        await using var db = NewDb();
        var workflowInstanceId = Guid.NewGuid();
        var activityName = "test-activity";

        var v1 = MakeStep("ValA", StepKind.Validation, ProcessStepResult.Pass());
        // Descriptor DisplayName is "ValA Display" (set in MakeStep above)

        db.ActivityProcessConfigurations.Add(
            MakeConfig(activityName, "ValA", StepKind.Validation, 1));
        await db.SaveChangesAsync();

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(MakeWorkflowInstance(workflowInstanceId));

        IReadOnlyList<StepInfo>? capturedSteps = null;
        var reporter = MakeReporter();
        reporter.WhenForAnyArgs(r => r.PipelineStarted(default, default!, default!, default!, default))
            .Do(callInfo => capturedSteps = callInfo.ArgAt<IReadOnlyList<StepInfo>>(2));

        var pipeline = BuildPipeline(db, instanceRepo, [v1], reporter);

        await pipeline.ExecuteAsync(
            workflowInstanceId, Guid.NewGuid(), activityName, "user", NoRoles, NoInput, NoAck, CancellationToken.None);

        capturedSteps.Should().NotBeNull();
        capturedSteps!.Should().HaveCount(1);
        capturedSteps[0].StepName.Should().Be("ValA");
        capturedSteps[0].DisplayName.Should().Be("ValA Display");
    }

    // ── NoOpActivityProgressReporter ──────────────────────────────────────

    // R6: NoOpActivityProgressReporter returns Task.CompletedTask for all methods
    [Fact]
    public async Task NoOpReporter_AllMethods_ReturnCompletedTask()
    {
        var noop = new NoOpActivityProgressReporter();
        var id = Guid.NewGuid();
        var step = new StepInfo("S", "S Display", 1, "Validation");

        await noop.PipelineStarted(id, "act", [], "user", CancellationToken.None);
        await noop.StepStarted(id, step, "user", CancellationToken.None);
        await noop.StepFinished(id, step, "Passed", 10, "user", CancellationToken.None);
        await noop.PipelineFinished(id, "Success", "user", CancellationToken.None);
        // Reaching here without exception = pass
    }
}
