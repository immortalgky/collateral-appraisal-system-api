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
/// Unit tests for ActivityProcessPipeline orchestration logic.
/// Uses an in-memory WorkflowDbContext so no real DB is required.
/// Steps are NSubstitute spies so invocation counts are verifiable.
/// </summary>
public class ActivityProcessPipelineTests
{
    // ── Infrastructure ────────────────────────────────────────────────────

    private static WorkflowDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"pipeline-{Guid.NewGuid()}")
            .Options;
        return new WorkflowDbContext(options);
    }

    private static IActivityProcessStep MakeStep(string name, StepKind kind, ProcessStepResult result)
    {
        var step = Substitute.For<IActivityProcessStep>();
        step.Descriptor.Returns(StepDescriptor.For<object>(name, name, kind));
        step.ExecuteAsync(Arg.Any<ProcessStepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));
        return step;
    }

    private static IActivityProcessStep MakeStep(string name, StepKind kind, Func<ProcessStepResult> resultFactory)
    {
        var step = Substitute.For<IActivityProcessStep>();
        step.Descriptor.Returns(StepDescriptor.For<object>(name, name, kind));
        step.ExecuteAsync(Arg.Any<ProcessStepContext>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(resultFactory()));
        return step;
    }

    private static ActivityProcessConfiguration MakeConfig(
        string activityName, string processorName, StepKind kind, int sortOrder, string? runIfExpression = null)
    {
        return ActivityProcessConfiguration.Create(
            activityName, processorName + " label", processorName, kind, sortOrder, "system", null, runIfExpression);
    }

    private static WorkflowInstance MakeWorkflowInstance(Guid id)
    {
        var instance = WorkflowInstance.Create(Guid.NewGuid(), "test-wf", null, "tester");
        // Entity<Guid>.Id has a public setter
        instance.Id = id;
        return instance;
    }

    private ActivityProcessPipeline BuildPipeline(
        WorkflowDbContext db,
        IWorkflowInstanceRepository instanceRepo,
        IPredicateEvaluator predicateEvaluator,
        IEnumerable<IActivityProcessStep> steps,
        IActivityProcessExecutionSink? sink = null)
    {
        sink ??= Substitute.For<IActivityProcessExecutionSink>();
        sink.PersistAsync(Arg.Any<IReadOnlyList<ActivityProcessExecution>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var reporter = Substitute.For<IActivityProgressReporter>();
        reporter.PipelineStarted(default, default!, default!, default!, default)
            .ReturnsForAnyArgs(Task.CompletedTask);
        reporter.StepStarted(default, default!, default!, default)
            .ReturnsForAnyArgs(Task.CompletedTask);
        reporter.StepFinished(default, default!, default!, default, default!, default)
            .ReturnsForAnyArgs(Task.CompletedTask);
        reporter.PipelineFinished(default, default!, default!, default)
            .ReturnsForAnyArgs(Task.CompletedTask);

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
    private static IReadOnlyDictionary<string, object?> NoInput =>
        new Dictionary<string, object?>();
    private static IReadOnlyCollection<string> NoAck => Array.Empty<string>();

    // ── Tests ─────────────────────────────────────────────────────────────

    // T1: Validations collect-all — two failures, Actions not invoked
    [Fact]
    public async Task Validations_CollectAll_BothFailures_NoActionsRun()
    {
        await using var db = NewDb();
        var workflowInstanceId = Guid.NewGuid();
        var activityName = "test-activity";

        // Two failing Validations
        var v1 = MakeStep("ValA", StepKind.Validation, ProcessStepResult.Fail("ERR_A", "Error A"));
        var v2 = MakeStep("ValB", StepKind.Validation, ProcessStepResult.Fail("ERR_B", "Error B"));
        var a1 = MakeStep("ActA", StepKind.Action, ProcessStepResult.Pass());

        db.ActivityProcessConfigurations.AddRange(
            MakeConfig(activityName, "ValA", StepKind.Validation, 1),
            MakeConfig(activityName, "ValB", StepKind.Validation, 2),
            MakeConfig(activityName, "ActA", StepKind.Action, 1));
        await db.SaveChangesAsync();

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(MakeWorkflowInstance(workflowInstanceId));

        var predicateEvaluator = Substitute.For<IPredicateEvaluator>();

        var pipeline = BuildPipeline(db, instanceRepo, predicateEvaluator, [v1, v2, a1]);

        var result = await pipeline.ExecuteAsync(
            workflowInstanceId, Guid.NewGuid(), activityName, "user", NoRoles, NoInput, NoAck, CancellationToken.None);

        // Both validation failures collected
        result.IsSuccess.Should().BeFalse();
        result.ValidationFailures.Should().HaveCount(2);
        result.ValidationFailures.Should().Contain(f => f.ErrorCode == "ERR_A");
        result.ValidationFailures.Should().Contain(f => f.ErrorCode == "ERR_B");
        result.ActionFailure.Should().BeNull();

        // Action was NOT invoked
        await a1.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
    }

    // T2: Actions stop-on-first — first action fails, second not invoked
    [Fact]
    public async Task Actions_StopOnFirst_SecondActionNotInvoked()
    {
        await using var db = NewDb();
        var workflowInstanceId = Guid.NewGuid();
        var activityName = "test-activity";

        var a1 = MakeStep("ActA", StepKind.Action, ProcessStepResult.Fail("ACT_FAIL", "Action A failed"));
        var a2 = MakeStep("ActB", StepKind.Action, ProcessStepResult.Pass());

        db.ActivityProcessConfigurations.AddRange(
            MakeConfig(activityName, "ActA", StepKind.Action, 1),
            MakeConfig(activityName, "ActB", StepKind.Action, 2));
        await db.SaveChangesAsync();

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(MakeWorkflowInstance(workflowInstanceId));

        var predicateEvaluator = Substitute.For<IPredicateEvaluator>();

        var pipeline = BuildPipeline(db, instanceRepo, predicateEvaluator, [a1, a2]);

        var result = await pipeline.ExecuteAsync(
            workflowInstanceId, Guid.NewGuid(), activityName, "user", NoRoles, NoInput, NoAck, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ActionFailure.Should().NotBeNull();
        result.ActionFailure!.ErrorCode.Should().Be("ACT_FAIL");
        result.ValidationFailures.Should().BeEmpty();

        // Second action was NOT invoked
        await a2.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
    }

    // T3: RunIfFalse skips step and traces Skipped(RunIfFalse)
    [Fact]
    public async Task RunIfFalse_SkipsStep_TracedAsRunIfFalse()
    {
        await using var db = NewDb();
        var workflowInstanceId = Guid.NewGuid();
        var activityName = "test-activity";

        var v1 = MakeStep("ValA", StepKind.Validation, ProcessStepResult.Pass());

        var config = MakeConfig(activityName, "ValA", StepKind.Validation, 1, runIfExpression: "false");
        db.ActivityProcessConfigurations.Add(config);
        await db.SaveChangesAsync();

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(MakeWorkflowInstance(workflowInstanceId));

        var predicateEvaluator = Substitute.For<IPredicateEvaluator>();
        predicateEvaluator.Evaluate("false", config.Id, config.Version, Arg.Any<ProcessStepContext>())
            .Returns(false);

        var tracedRows = new List<ActivityProcessExecution>();
        var sink = Substitute.For<IActivityProcessExecutionSink>();
        sink.When(s => s.PersistAsync(Arg.Any<IReadOnlyList<ActivityProcessExecution>>(), Arg.Any<CancellationToken>()))
            .Do(ci => tracedRows.AddRange(ci.Arg<IReadOnlyList<ActivityProcessExecution>>()));

        var pipeline = BuildPipeline(db, instanceRepo, predicateEvaluator, [v1], sink);

        var result = await pipeline.ExecuteAsync(
            workflowInstanceId, Guid.NewGuid(), activityName, "user", NoRoles, NoInput, NoAck, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // The step was skipped — actual execution not called
        await v1.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
        // Trace was recorded as Skipped with RunIfFalse reason
        tracedRows.Should().ContainSingle(r =>
            r.Outcome == StepOutcome.Skipped && r.SkipReason == Data.Entities.SkipReason.RunIfFalse);
    }

    // T4: Malformed RunIfExpression halts the pipeline with ExpressionError
    [Fact]
    public async Task ExpressionError_HaltsPipeline_TracedAsErrored()
    {
        await using var db = NewDb();
        var workflowInstanceId = Guid.NewGuid();
        var activityName = "test-activity";

        var v1 = MakeStep("ValA", StepKind.Validation, ProcessStepResult.Pass());

        var config = MakeConfig(activityName, "ValA", StepKind.Validation, 1, runIfExpression: "!!!invalid");
        db.ActivityProcessConfigurations.Add(config);
        await db.SaveChangesAsync();

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(MakeWorkflowInstance(workflowInstanceId));

        var predicateEvaluator = Substitute.For<IPredicateEvaluator>();
        predicateEvaluator.Evaluate("!!!invalid", config.Id, config.Version, Arg.Any<ProcessStepContext>())
            .Throws(new PredicateEvaluationException("parse error"));

        var tracedRows = new List<ActivityProcessExecution>();
        var sink = Substitute.For<IActivityProcessExecutionSink>();
        sink.When(s => s.PersistAsync(Arg.Any<IReadOnlyList<ActivityProcessExecution>>(), Arg.Any<CancellationToken>()))
            .Do(ci => tracedRows.AddRange(ci.Arg<IReadOnlyList<ActivityProcessExecution>>()));

        var pipeline = BuildPipeline(db, instanceRepo, predicateEvaluator, [v1], sink);

        var result = await pipeline.ExecuteAsync(
            workflowInstanceId, Guid.NewGuid(), activityName, "user", NoRoles, NoInput, NoAck, CancellationToken.None);

        // Pipeline fails (expression error is treated as a step failure)
        result.IsSuccess.Should().BeFalse();
        // Trace shows ExpressionError
        tracedRows.Should().ContainSingle(r =>
            r.SkipReason == Data.Entities.SkipReason.ExpressionError);
    }

    // T5: Kind ordering — Validations run before Actions even with higher SortOrder
    [Fact]
    public async Task KindOrdering_ValidationsRunFirst_EvenIfSortOrderIsHigher()
    {
        await using var db = NewDb();
        var workflowInstanceId = Guid.NewGuid();
        var activityName = "test-activity";

        // Both pass so we can verify both were called and in correct order
        var executionOrder = new List<string>();

        var v1 = Substitute.For<IActivityProcessStep>();
        v1.Descriptor.Returns(StepDescriptor.For<object>("ValA", "ValA", StepKind.Validation));
        v1.ExecuteAsync(Arg.Any<ProcessStepContext>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                executionOrder.Add("ValA");
                return Task.FromResult(ProcessStepResult.Pass());
            });

        var a1 = Substitute.For<IActivityProcessStep>();
        a1.Descriptor.Returns(StepDescriptor.For<object>("ActA", "ActA", StepKind.Action));
        a1.ExecuteAsync(Arg.Any<ProcessStepContext>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                executionOrder.Add("ActA");
                return Task.FromResult(ProcessStepResult.Pass());
            });

        // Action has SortOrder=0 (lower than Validation's SortOrder=100),
        // but Validations phase must still run first.
        db.ActivityProcessConfigurations.AddRange(
            MakeConfig(activityName, "ActA", StepKind.Action, 0),
            MakeConfig(activityName, "ValA", StepKind.Validation, 100));
        await db.SaveChangesAsync();

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(MakeWorkflowInstance(workflowInstanceId));
        instanceRepo.UpdateAsync(Arg.Any<WorkflowInstance>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var predicateEvaluator = Substitute.For<IPredicateEvaluator>();

        var pipeline = BuildPipeline(db, instanceRepo, predicateEvaluator, [v1, a1]);

        var result = await pipeline.ExecuteAsync(
            workflowInstanceId, Guid.NewGuid(), activityName, "user", NoRoles, NoInput, NoAck, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Validation ran before Action regardless of SortOrder
        executionOrder.Should().Equal("ValA", "ActA");
    }

    // ── Severity / warning acknowledge-to-continue ─────────────────────────

    private static ActivityProcessConfiguration MakeConfig(
        string activityName, string processorName, StepKind kind, int sortOrder,
        StepSeverity severity, string? runIfExpression = null)
    {
        return ActivityProcessConfiguration.Create(
            activityName, processorName + " label", processorName, kind, sortOrder,
            "system", null, runIfExpression, severity);
    }

    // D: an unknown ProcessorName must BLOCK (fail-closed), never silently pass.
    [Fact]
    public async Task UnknownStep_FailsClosed_BlocksCompletion()
    {
        await using var db = NewDb();
        var workflowInstanceId = Guid.NewGuid();
        var activityName = "test-activity";

        // Config references a processor with no registered step.
        db.ActivityProcessConfigurations.Add(
            MakeConfig(activityName, "GhostStep", StepKind.Validation, 1));
        await db.SaveChangesAsync();

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(MakeWorkflowInstance(workflowInstanceId));

        // No steps registered → GhostStep cannot resolve.
        var pipeline = BuildPipeline(db, instanceRepo, Substitute.For<IPredicateEvaluator>(), []);

        var result = await pipeline.ExecuteAsync(
            workflowInstanceId, Guid.NewGuid(), activityName, "user", NoRoles, NoInput, NoAck, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ValidationFailures.Should().ContainSingle(f => f.ErrorCode == "StepNotFound");
    }

    // C2: a Warning-only failure with no acknowledgement → WarningsPending, no Actions, no mutation.
    [Fact]
    public async Task WarningOnly_NoAck_ReturnsWarningsPending_NoActions()
    {
        await using var db = NewDb();
        var workflowInstanceId = Guid.NewGuid();
        var activityName = "test-activity";

        var v1 = MakeStep("WarnA", StepKind.Validation, ProcessStepResult.Fail("SOFT", "soft issue"));
        var a1 = MakeStep("ActA", StepKind.Action, ProcessStepResult.Pass());

        db.ActivityProcessConfigurations.AddRange(
            MakeConfig(activityName, "WarnA", StepKind.Validation, 1, StepSeverity.Warning),
            MakeConfig(activityName, "ActA", StepKind.Action, 1));
        await db.SaveChangesAsync();

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(MakeWorkflowInstance(workflowInstanceId));

        var pipeline = BuildPipeline(db, instanceRepo, Substitute.For<IPredicateEvaluator>(), [v1, a1]);

        var result = await pipeline.ExecuteAsync(
            workflowInstanceId, Guid.NewGuid(), activityName, "user", NoRoles, NoInput, NoAck, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.RequiresAcknowledgement.Should().BeTrue();
        result.Warnings.Should().ContainSingle().Which.AckToken.Should().NotBeNullOrEmpty();
        result.ValidationFailures.Should().BeEmpty();
        await a1.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
    }

    // C2: acknowledging the exact warning token lets the pipeline proceed to Actions.
    [Fact]
    public async Task WarningOnly_WithMatchingToken_Proceeds()
    {
        await using var db = NewDb();
        var workflowInstanceId = Guid.NewGuid();
        var activityName = "test-activity";

        var v1 = MakeStep("WarnA", StepKind.Validation, ProcessStepResult.Fail("SOFT", "soft issue"));
        var a1 = MakeStep("ActA", StepKind.Action, ProcessStepResult.Pass());

        db.ActivityProcessConfigurations.AddRange(
            MakeConfig(activityName, "WarnA", StepKind.Validation, 1, StepSeverity.Warning),
            MakeConfig(activityName, "ActA", StepKind.Action, 1));
        await db.SaveChangesAsync();

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(MakeWorkflowInstance(workflowInstanceId));
        instanceRepo.UpdateAsync(Arg.Any<WorkflowInstance>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var pipeline = BuildPipeline(db, instanceRepo, Substitute.For<IPredicateEvaluator>(), [v1, a1]);

        // First run surfaces the token...
        var first = await pipeline.ExecuteAsync(
            workflowInstanceId, Guid.NewGuid(), activityName, "user", NoRoles, NoInput, NoAck, CancellationToken.None);
        var token = first.Warnings.Single().AckToken!;

        // ...re-run acknowledging it → proceeds.
        var second = await pipeline.ExecuteAsync(
            workflowInstanceId, Guid.NewGuid(), activityName, "user", NoRoles, NoInput, [token], CancellationToken.None);

        second.IsSuccess.Should().BeTrue();
        await a1.ReceivedWithAnyArgs(1).ExecuteAsync(default!, default);
    }

    // C2: a newly-surfaced warning (different token) is not covered by a stale acknowledgement.
    [Fact]
    public async Task NewWarning_NotAcknowledged_RePrompts()
    {
        await using var db = NewDb();
        var workflowInstanceId = Guid.NewGuid();
        var activityName = "test-activity";

        var warnA = MakeStep("WarnA", StepKind.Validation, ProcessStepResult.Fail("SOFT_A", "soft A"));
        var warnB = MakeStep("WarnB", StepKind.Validation, ProcessStepResult.Fail("SOFT_B", "soft B"));

        db.ActivityProcessConfigurations.AddRange(
            MakeConfig(activityName, "WarnA", StepKind.Validation, 1, StepSeverity.Warning),
            MakeConfig(activityName, "WarnB", StepKind.Validation, 2, StepSeverity.Warning));
        await db.SaveChangesAsync();

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(MakeWorkflowInstance(workflowInstanceId));

        var pipeline = BuildPipeline(db, instanceRepo, Substitute.For<IPredicateEvaluator>(), [warnA, warnB]);

        var first = await pipeline.ExecuteAsync(
            workflowInstanceId, Guid.NewGuid(), activityName, "user", NoRoles, NoInput, NoAck, CancellationToken.None);
        var tokenA = first.Warnings.First(w => w.StepName == "WarnA").AckToken!;

        // Acknowledge only WarnA's token → WarnB still pending → re-prompt.
        var second = await pipeline.ExecuteAsync(
            workflowInstanceId, Guid.NewGuid(), activityName, "user", NoRoles, NoInput, [tokenA], CancellationToken.None);

        second.RequiresAcknowledgement.Should().BeTrue();
        second.Warnings.Should().Contain(w => w.StepName == "WarnB");
    }

    // E: a forward-only validation (RunIf activity.movement === 'F') is skipped on route-back.
    [Theory]
    [InlineData("R", true)]   // Route Back → movement B → forward-only check skipped → success
    [InlineData("P", false)]  // Proceed   → movement F → forward-only check runs → blocked
    public async Task MovementGate_ForwardOnlyValidation_SkipsOnRouteBack(string decision, bool shouldSucceed)
    {
        await using var db = NewDb();
        var workflowInstanceId = Guid.NewGuid();
        var activityName = "ext-appraisal-execution";

        // A validation that always fails — gated to forward movement only.
        var v1 = MakeStep("BlockingVal", StepKind.Validation, ProcessStepResult.Fail("HARD", "missing data"));
        db.ActivityProcessConfigurations.Add(
            MakeConfig(activityName, "BlockingVal", StepKind.Validation, 1,
                StepSeverity.Error, runIfExpression: "activity.movement === 'F'"));
        await db.SaveChangesAsync();

        // Definition declares P=Forward, R=Backward for this activity.
        const string definitionJson = """
            {"activities":[{"id":"ext-appraisal-execution","properties":{"actions":[
              {"value":"P","movement":"F"},
              {"value":"R","movement":"B"}
            ]}}]}
            """;
        var instance = BuildInstanceWithDefinition(workflowInstanceId, definitionJson);

        var instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        instanceRepo.GetByIdAsync(workflowInstanceId, Arg.Any<CancellationToken>()).Returns(instance);

        // Use the REAL Jint evaluator so activity.movement gating is exercised end-to-end.
        var pipeline = BuildPipeline(db, instanceRepo, new JintPredicateEvaluator(), [v1]);

        // The caller sends the BARE key "decisionTaken" (matches TaskActivity + the real FE contract).
        var input = new Dictionary<string, object?>
        {
            ["decisionTaken"] = decision
        };

        var result = await pipeline.ExecuteAsync(
            workflowInstanceId, Guid.NewGuid(), activityName, "user", NoRoles, input, NoAck, CancellationToken.None);

        result.IsSuccess.Should().Be(shouldSucceed);
    }

    private static WorkflowInstance BuildInstanceWithDefinition(Guid id, string definitionJson)
    {
        var definition = global::Workflow.Workflow.Models.WorkflowDefinition.Create(
            "test-wf", "desc", definitionJson, "Test", "tester");
        var instance = WorkflowInstance.Create(definition.Id, "wf-1", null, "tester");
        instance.Id = id;
        typeof(WorkflowInstance).GetProperty(nameof(WorkflowInstance.WorkflowDefinition))!
            .SetValue(instance, definition);
        return instance;
    }
}
