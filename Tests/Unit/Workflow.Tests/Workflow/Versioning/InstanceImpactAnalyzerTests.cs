using FluentAssertions;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Versioning.SchemaDiffing;
using Xunit;
using InstanceImpactAnalyzer = Workflow.Workflow.Versioning.InstanceImpactAnalyzer;
using BreakingChange = Workflow.Workflow.Models.BreakingChange;

namespace Workflow.Tests.Workflow.Versioning;

public class InstanceImpactAnalyzerTests
{
    private readonly InstanceImpactAnalyzer _analyzer = new();
    private readonly WorkflowSchemaDiffer _differ = new();

    [Fact]
    public void Classify_UnaffectedInstance_IsSafe()
    {
        var oldSchema = SimpleChain("a", "b", "c");
        var newSchema = SimpleChain("a", "b", "c", "d"); // pure addition — no breaking change
        var changes = _differ.Diff(oldSchema, newSchema);

        var instance = InstanceAt("a");

        var report = _analyzer.Analyze(new[] { instance }, oldSchema, newSchema, changes);

        report.SafeCount.Should().Be(1);
        report.UnsafeCount.Should().Be(0);
    }

    [Fact]
    public void Classify_InstanceAtRemovedNode_IsUnsafe()
    {
        var oldSchema = SimpleChain("a", "b", "c");
        // b removed in new schema
        var newSchema = BuildSchema(
            new[] { Activity("a"), Activity("c") },
            new[] { Transition("a", "c") });

        var changes = _differ.Diff(oldSchema, newSchema);
        var instance = InstanceAt("b");

        var report = _analyzer.Analyze(new[] { instance }, oldSchema, newSchema, changes);

        report.UnsafeCount.Should().Be(1);
        report.SafeCount.Should().Be(0);
    }

    [Fact]
    public void Classify_InstanceWithDownstreamRemovedNode_IsUnsafe()
    {
        var oldSchema = SimpleChain("a", "b", "c");
        // c removed — instance at "a" will reach it downstream.
        var newSchema = BuildSchema(
            new[] { Activity("a"), Activity("b") },
            new[] { Transition("a", "b") });

        var changes = _differ.Diff(oldSchema, newSchema);
        var instance = InstanceAt("a");

        var report = _analyzer.Analyze(new[] { instance }, oldSchema, newSchema, changes);

        report.UnsafeCount.Should().Be(1);
    }

    [Fact]
    public void Classify_OnlyPathRemovedViaTransition_IsUnsafe()
    {
        var oldSchema = BuildSchema(
            new[] { Activity("a"), Activity("b") },
            new[] { Transition("a", "b") });

        // transition removed, activity still exists in new schema
        var newSchema = BuildSchema(
            new[] { Activity("a"), Activity("b") },
            Array.Empty<TransitionDefinition>());

        var changes = _differ.Diff(oldSchema, newSchema);
        var instance = InstanceAt("a");

        var report = _analyzer.Analyze(new[] { instance }, oldSchema, newSchema, changes);

        report.UnsafeCount.Should().Be(1);
    }

    [Fact]
    public void ComputeBreakingChangeHash_StableForPermutedInputs()
    {
        var changes1 = new List<BreakingChange>
        {
            BreakingChange.ActivityRemoved("b", "b removed"),
            BreakingChange.ActivityRemoved("a", "a removed")
        };
        var changes2 = new List<BreakingChange>
        {
            BreakingChange.ActivityRemoved("a", "a removed"),
            BreakingChange.ActivityRemoved("b", "b removed")
        };

        var hash1 = _analyzer.ComputeBreakingChangeHash(changes1);
        var hash2 = _analyzer.ComputeBreakingChangeHash(changes2);

        hash1.Should().NotBeEmpty();
        hash1.Should().Be(hash2);
    }

    private static WorkflowInstance InstanceAt(string activityId)
    {
        var instance = WorkflowInstance.Create(Guid.NewGuid(), Guid.NewGuid(), "test", null, "tester");
        instance.SetCurrentActivity(activityId);
        return instance;
    }

    private static ActivityDefinition Activity(string id) => new()
    {
        Id = id,
        Name = id,
        Type = "TaskActivity",
        Description = id
    };

    private static TransitionDefinition Transition(string from, string to) => new()
    {
        Id = $"{from}->{to}",
        From = from,
        To = to
    };

    private static WorkflowSchema SimpleChain(params string[] ids)
    {
        var activities = ids.Select(Activity).ToList();
        var transitions = new List<TransitionDefinition>();
        for (var i = 0; i < ids.Length - 1; i++)
            transitions.Add(Transition(ids[i], ids[i + 1]));
        return BuildSchema(activities, transitions);
    }

    private static WorkflowSchema BuildSchema(
        IReadOnlyList<ActivityDefinition> activities,
        IReadOnlyList<TransitionDefinition> transitions)
        => new()
        {
            Id = "test",
            Name = "test",
            Description = "test",
            Category = "test",
            Activities = activities.ToList(),
            Transitions = transitions.ToList()
        };
}
