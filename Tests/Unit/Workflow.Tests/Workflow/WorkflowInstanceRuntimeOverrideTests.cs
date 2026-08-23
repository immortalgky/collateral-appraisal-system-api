using FluentAssertions;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;

namespace Workflow.Tests.Workflow;

/// <summary>
/// Guards the one-shot semantics of <see cref="WorkflowInstance.RuntimeOverrides"/>.
/// The API field is <c>NextAssignmentOverrides</c> — it names who does an activity NEXT, not
/// forever. A pin left behind keeps winning on every re-entry, because
/// <c>AssignmentPipeline.SelectAssigneeAsync</c> short-circuits to ManualPick before it ever looks
/// at <c>revisitAssignmentStrategies</c>, so <c>previous_owner</c> never runs and a route-back
/// lands on the originally picked user instead of whoever actually did the work last.
/// <see cref="WorkflowInstance.ClearRuntimeOverride"/> is the consumption point; TaskActivity calls
/// it right after a successful assignment.
/// </summary>
public class WorkflowInstanceRuntimeOverrideTests
{
    private static WorkflowInstance NewInstance(Dictionary<string, RuntimeOverride>? overrides = null) =>
        WorkflowInstance.Create(
            Guid.NewGuid(), "test-workflow", correlationId: Guid.NewGuid().ToString(),
            startedBy: "tester", runtimeOverrides: overrides);

    private static RuntimeOverride Pin(string assignee) =>
        RuntimeOverride.ForAssignee(assignee, "Admin-selected internal appraiser", "supervisor1");

    [Fact]
    public void ClearRuntimeOverride_RemovesOnlyTheNamedActivity()
    {
        var instance = NewInstance(new Dictionary<string, RuntimeOverride>
        {
            ["ext-appraisal-execution"] = Pin("int.staff1"),
            ["ext-appraisal-check"] = Pin("int.checker1")
        });

        instance.ClearRuntimeOverride("ext-appraisal-execution");

        instance.RuntimeOverrides.Should().NotContainKey("ext-appraisal-execution");
        instance.RuntimeOverrides.Should().ContainKey("ext-appraisal-check",
            "consuming one activity's pin must not disturb a pin queued for another activity");
    }

    [Fact]
    public void ClearRuntimeOverride_ReplacesTheDictionaryReference()
    {
        var instance = NewInstance(new Dictionary<string, RuntimeOverride>
        {
            ["ext-appraisal-execution"] = Pin("int.staff1")
        });
        var before = instance.RuntimeOverrides;

        instance.ClearRuntimeOverride("ext-appraisal-execution");

        instance.RuntimeOverrides.Should().NotBeSameAs(before,
            "RuntimeOverrides has a JSON value converter but no value comparer, so EF only detects a change by reference");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-pinned-activity")]
    public void ClearRuntimeOverride_NoMatchingPin_IsNoOp(string activityId)
    {
        var instance = NewInstance(new Dictionary<string, RuntimeOverride>
        {
            ["ext-appraisal-execution"] = Pin("int.staff1")
        });
        var before = instance.RuntimeOverrides;

        instance.ClearRuntimeOverride(activityId);

        instance.RuntimeOverrides.Should().BeSameAs(before, "a no-op must not churn the reference");
        instance.RuntimeOverrides.Should().ContainKey("ext-appraisal-execution");
    }

    [Fact]
    public void ClearRuntimeOverride_ThenNewPin_IsHonouredAgain()
    {
        // A later manual pick simply writes a fresh entry, which is honoured and consumed in turn.
        var instance = NewInstance(new Dictionary<string, RuntimeOverride>
        {
            ["ext-appraisal-execution"] = Pin("int.staff1")
        });

        instance.ClearRuntimeOverride("ext-appraisal-execution");
        instance.UpdateRuntimeOverrides(new Dictionary<string, RuntimeOverride>
        {
            ["ext-appraisal-execution"] = Pin("int.staff2")
        });

        instance.RuntimeOverrides["ext-appraisal-execution"].RuntimeAssignee.Should().Be("int.staff2");
    }
}
