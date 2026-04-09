using FluentAssertions;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Versioning.SchemaDiffing;
using Xunit;

namespace Workflow.Tests.Workflow.Versioning;

public class WorkflowSchemaDifferTests
{
    private readonly WorkflowSchemaDiffer _differ = new();

    [Fact]
    public void Diff_IdenticalSchemas_ReturnsEmpty()
    {
        var schema = BuildSchema(
            activities: new[] { Activity("a", "TaskActivity"), Activity("b", "TaskActivity") },
            transitions: new[] { Transition("a", "b") });

        var result = _differ.Diff(schema, schema);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Diff_ActivityRemoved_EmitsActivityRemoved()
    {
        var oldSchema = BuildSchema(
            activities: new[] { Activity("a", "TaskActivity"), Activity("b", "TaskActivity") },
            transitions: new[] { Transition("a", "b") });

        var newSchema = BuildSchema(
            activities: new[] { Activity("a", "TaskActivity") },
            transitions: Array.Empty<TransitionDefinition>());

        var result = _differ.Diff(oldSchema, newSchema);

        result.Should().Contain(c => c.Type == "ActivityRemoved" && c.AffectedComponent == "b");
    }

    [Fact]
    public void Diff_ActivityTypeChanged_EmitsPropertyChangedForType()
    {
        var oldSchema = BuildSchema(
            activities: new[] { Activity("a", "TaskActivity") },
            transitions: Array.Empty<TransitionDefinition>());

        var newSchema = BuildSchema(
            activities: new[] { Activity("a", "ServiceActivity") },
            transitions: Array.Empty<TransitionDefinition>());

        var result = _differ.Diff(oldSchema, newSchema);

        result.Should().ContainSingle(c =>
            c.Type == "PropertyChanged" &&
            c.MigrationData["PropertyName"].ToString() == "Type" &&
            c.MigrationData["ActivityId"].ToString() == "a");
    }

    [Fact]
    public void Diff_RequiredPropertyAdded_EmitsPropertyChanged()
    {
        var oldActivity = Activity("a", "TaskActivity");
        var newActivity = Activity("a", "TaskActivity");
        newActivity.Properties["assignee"] = new Dictionary<string, object>
        {
            ["required"] = true
        };

        var oldSchema = BuildSchema(new[] { oldActivity }, Array.Empty<TransitionDefinition>());
        var newSchema = BuildSchema(new[] { newActivity }, Array.Empty<TransitionDefinition>());

        var result = _differ.Diff(oldSchema, newSchema);

        result.Should().Contain(c =>
            c.Type == "PropertyChanged" &&
            c.MigrationData["PropertyName"].ToString() == "assignee");
    }

    [Fact]
    public void Diff_RequiredPropertyWithDefault_NotBreaking()
    {
        var oldActivity = Activity("a", "TaskActivity");
        var newActivity = Activity("a", "TaskActivity");
        newActivity.Properties["assignee"] = new Dictionary<string, object>
        {
            ["required"] = true,
            ["default"] = "system"
        };

        var result = _differ.Diff(
            BuildSchema(new[] { oldActivity }, Array.Empty<TransitionDefinition>()),
            BuildSchema(new[] { newActivity }, Array.Empty<TransitionDefinition>()));

        result.Should().BeEmpty();
    }

    [Fact]
    public void Diff_TransitionRemovedFromExistingSource_EmitsBreakingChange()
    {
        var oldSchema = BuildSchema(
            activities: new[] { Activity("a", "TaskActivity"), Activity("b", "TaskActivity") },
            transitions: new[] { Transition("a", "b") });

        var newSchema = BuildSchema(
            activities: new[] { Activity("a", "TaskActivity"), Activity("b", "TaskActivity") },
            transitions: Array.Empty<TransitionDefinition>());

        var result = _differ.Diff(oldSchema, newSchema);

        result.Should().ContainSingle(c => c.Type == "TransitionRemoved" && c.AffectedComponent == "a");
    }

    [Fact]
    public void Diff_PureAdditions_NotBreaking()
    {
        var oldSchema = BuildSchema(
            activities: new[] { Activity("a", "TaskActivity") },
            transitions: Array.Empty<TransitionDefinition>());

        var newSchema = BuildSchema(
            activities: new[] { Activity("a", "TaskActivity"), Activity("b", "TaskActivity") },
            transitions: new[] { Transition("a", "b") });

        var result = _differ.Diff(oldSchema, newSchema);

        result.Should().BeEmpty();
    }

    private static ActivityDefinition Activity(string id, string type) => new()
    {
        Id = id,
        Name = id,
        Type = type,
        Description = id
    };

    private static TransitionDefinition Transition(string from, string to, string? condition = null) => new()
    {
        Id = $"{from}->{to}",
        From = from,
        To = to,
        Condition = condition
    };

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
