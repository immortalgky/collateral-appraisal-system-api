using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Identity;
using Workflow.Services.Configuration.Models;
using Workflow.Data;
using FluentAssertions;
using Workflow.Services.Configuration;
using Workflow.Workflow.Schema;
using Xunit;

namespace Workflow.Tests.Services.Configuration;

public class TaskAssignmentConfigValidationTests
{
    // Runs the two stages the way the endpoints do: definition-free checks, then the definition-side ones.
    private static string? ValidateBoth(
        string? activityId, string? bankingSegment,
        List<string>? primaryStrategies, List<string>? routeBackStrategies,
        Dictionary<string, object>? additionalConfiguration = null,
        IReadOnlyList<ActivityDefinition>? definitionActivities = null,
        string? specificAssignee = null,
        string? assigneeGroup = null,
        bool? teamConstrained = null,
        List<string>? excludeAssigneesFrom = null)
        => TaskAssignmentConfigAdminEndpoints.ValidateBasics(
               activityId, bankingSegment, primaryStrategies, routeBackStrategies, additionalConfiguration)
           ?? TaskAssignmentConfigAdminEndpoints.ValidateAgainstDefinition(
               new TaskAssignmentConfigAdminEndpoints.OverrideToValidate(
                   activityId, primaryStrategies, routeBackStrategies, additionalConfiguration,
                   specificAssignee, assigneeGroup, teamConstrained, excludeAssigneesFrom),
               definitionActivities);

    private static readonly IReadOnlyList<ActivityDefinition> Known =
    [
        new() { Id = "int-pma-input", Type = "TaskActivity" },
        new() { Id = "appraisal-book-verification", Type = "TaskActivity" }
    ];

    private static string? Validate(
        object? sameAssignee,
        string activityId = "appraisal-book-verification",
        IReadOnlyList<ActivityDefinition>? known = null)
        => ValidateBoth(
            activityId, null, null, null,
            new Dictionary<string, object> { ["sameAssigneeAsActivity"] = sameAssignee! },
            known);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_SameAssigneeAsActivity_RejectsEmptyString(object? value)
        => Validate(value).Should().Contain("non-empty");

    [Fact]
    public void Validate_SameAssigneeAsActivity_ExplicitNullMeansNotSet()
    {
        Validate(null).Should().BeNull();
        Validate(JsonDocument.Parse("null").RootElement).Should().BeNull();
    }

    [Fact]
    public void Validate_SameAssigneeAsActivity_RejectsSelf()
        => Validate("appraisal-book-verification", known: Known).Should().Contain("cannot be the activity");

    [Fact]
    public void Validate_SameAssigneeAsActivity_RejectsUnknownActivity()
        => Validate("no-such-activity", known: Known).Should().Contain("Unknown activity");

    [Fact]
    public void Validate_SameAssigneeAsActivity_AcceptsKnownStringAndJsonString()
    {
        Validate("int-pma-input", known: Known).Should().BeNull();
        Validate(JsonDocument.Parse("\"int-pma-input\"").RootElement, known: Known).Should().BeNull();
    }

    [Fact]
    public void Validate_SameAssigneeAsActivity_SkipsExistenceCheckWhenDefinitionUnavailable()
        => Validate("anything", known: null).Should().BeNull();

    // ── source required when same_assignee_as_activity is in the effective strategy list ──

    private static string? ValidateStrategies(
        List<string>? primary, List<string>? routeBack,
        Dictionary<string, object>? additional,
        params ActivityDefinition[]? definition)
        => ValidateBoth(
            "appraisal-book-verification", null, primary, routeBack, additional, definition);

    private static ActivityDefinition BookVerification(string? jsonSource = null, string? jsonInitial = null)
    {
        var props = new Dictionary<string, object>();
        if (jsonSource is not null) props["sameAssigneeAsActivity"] = jsonSource;
        if (jsonInitial is not null) props["initialAssignmentStrategies"] = new List<string> { jsonInitial };
        return new ActivityDefinition { Id = "appraisal-book-verification", Type = "TaskActivity", Properties = props };
    }

    [Fact]
    public void Validate_StrategyWithoutSource_Rejected()
        => ValidateStrategies(["same_assignee_as_activity"], null, null, BookVerification())
            .Should().Contain("needs a source activity");

    [Fact]
    public void Validate_RouteBackStrategyWithoutSource_Rejected()
        => ValidateStrategies(null, ["SAME_ASSIGNEE_AS_ACTIVITY"], null, BookVerification())
            .Should().Contain("needs a source activity");

    [Fact]
    public void Validate_JsonStrategyWithoutSource_RejectedWhenOverrideListEmpty()
        => ValidateStrategies([], null, null, BookVerification(jsonInitial: "same_assignee_as_activity"))
            .Should().Contain("needs a source activity");

    [Fact]
    public void Validate_StrategyWithOverrideSource_Passes()
        => ValidateStrategies(
                ["same_assignee_as_activity"], null,
                new Dictionary<string, object> { ["sameAssigneeAsActivity"] = "int-pma-input" },
                [.. Known])
            .Should().BeNull();

    [Fact]
    public void Validate_StrategyWithJsonSource_Passes()
        => ValidateStrategies(["same_assignee_as_activity"], null, null, BookVerification(jsonSource: "int-pma-input"))
            .Should().BeNull();

    [Fact]
    public void Validate_StrategyNotUsed_Passes()
        => ValidateStrategies(["round_robin"], null, null, BookVerification()).Should().BeNull();

    [Fact]
    public void Validate_DefinitionUnavailable_StillRejectsOverrideTokenWithoutSourceKey()
        => ValidateBoth(
                "appraisal-book-verification", null, ["same_assignee_as_activity"], null, null, null)
            .Should().Contain("needs a source activity");

    [Theory]
    [InlineData(42)]
    [InlineData(true)]
    public void Validate_SameAssigneeAsActivity_RejectsNonString_EvenWithoutDefinition(object value)
        => Validate(value, known: null).Should().Contain("must be a string");

    [Fact]
    public void Validate_SameAssigneeAsActivity_RejectsJsonNumberAndObject_EvenWithoutDefinition()
    {
        Validate(JsonDocument.Parse("42").RootElement).Should().Contain("must be a string");
        Validate(JsonDocument.Parse("{\"a\":1}").RootElement).Should().Contain("must be a string");
        Validate(JsonDocument.Parse("[\"a\"]").RootElement).Should().Contain("must be a string");
    }

    [Fact]
    public void Validate_EmptyOverrideListWithSpecificAssignee_EffectiveStrategyIsManualNotJsonBaseline()
        => ValidateBoth(
                "appraisal-book-verification", null, [], [], null,
                [BookVerification(jsonInitial: "same_assignee_as_activity")], "somchai")
            .Should().BeNull();

    // ── InternalFollowupSelectionActivity overrides ──

    private static ActivityDefinition FollowupSelection(string? jsonSource = null) => new()
    {
        Id = "internal-followup-selection",
        Type = "InternalFollowupSelectionActivity",
        Properties = jsonSource is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["sameAssigneeAsActivity"] = jsonSource }
    };

    private static string? ValidateFollowup(
        List<string>? primary, List<string>? routeBack, Dictionary<string, object>? additional)
        => ValidateBoth(
            "internal-followup-selection", null, primary, routeBack, additional,
            [FollowupSelection(), new ActivityDefinition { Id = "int-pma-input", Type = "TaskActivity" }]);

    [Fact]
    public void Validate_FollowupSelection_RejectsStrategies()
    {
        ValidateFollowup(["round_robin"], null, null).Should().Contain("no assignment strategies");
        ValidateFollowup(null, ["previous_owner"], null).Should().Contain("no assignment strategies");
    }

    [Fact]
    public void Validate_FollowupSelection_SourceRuleDoesNotApplyButSelfAndExistenceDo()
    {
        ValidateFollowup([], [], null).Should().BeNull();
        ValidateFollowup([], [], new() { ["sameAssigneeAsActivity"] = "int-pma-input" }).Should().BeNull();
        ValidateFollowup([], [], new() { ["sameAssigneeAsActivity"] = "internal-followup-selection" })
            .Should().Contain("cannot be the activity");
        ValidateFollowup([], [], new() { ["sameAssigneeAsActivity"] = "nope" }).Should().Contain("Unknown activity");
    }

    [Fact]
    public void Validate_SameAssigneeAsActivity_RejectsAutomaticActivityAsSource()
        => ValidateBoth(
                "appraisal-book-verification", null, null, null,
                new Dictionary<string, object> { ["sameAssigneeAsActivity"] = "internal-followup-selection" },
                [.. Known, FollowupSelection()])
            .Should().Contain("must be a TaskActivity");

    [Fact]
    public void ValidateBasics_NeedsNoDefinition_RejectsBadShapeAndSelf()
    {
        TaskAssignmentConfigAdminEndpoints.ValidateBasics(
                "a", null, null, null, new() { ["sameAssigneeAsActivity"] = 42 })
            .Should().Contain("must be a string");
        TaskAssignmentConfigAdminEndpoints.ValidateBasics(
                "a", null, null, null, new() { ["sameAssigneeAsActivity"] = "a" })
            .Should().Contain("cannot be the activity");
        TaskAssignmentConfigAdminEndpoints.ValidateBasics(
                "a", null, null, null, new() { ["sameAssigneeAsActivity"] = "b" })
            .Should().BeNull();
    }

    [Fact]
    public void Validate_SameAssigneeAsActivity_RejectsFanOutTaskActivityAsSource()
        => ValidateBoth(
                "appraisal-book-verification", null, null, null,
                new Dictionary<string, object> { ["sameAssigneeAsActivity"] = "ext-fan-out" },
                [.. Known, new ActivityDefinition { Id = "ext-fan-out", Type = "FanOutTaskActivity" }])
            .Should().Contain("must be a TaskActivity");

    [Fact]
    public void Validate_SameAssigneeAsActivity_ExistingNonTaskSourceGetsTypeErrorNotUnknown()
        => ValidateBoth(
                "appraisal-book-verification", null, null, null,
                new Dictionary<string, object> { ["sameAssigneeAsActivity"] = "some-gateway" },
                [.. Known, new ActivityDefinition { Id = "some-gateway", Type = "DecisionActivity" }])
            .Should().Contain("must be a TaskActivity");

    // ── source vs excludeAssigneesFrom ──

    private static string? ValidateWithExclusions(
        List<string>? overrideExclusions, ActivityDefinition activity, string strategy = "same_assignee_as_activity")
        => ValidateBoth(
            "appraisal-book-verification", null, [strategy], null,
            new Dictionary<string, object> { ["sameAssigneeAsActivity"] = "int-pma-input" },
            [activity, new ActivityDefinition { Id = "int-pma-input", Type = "TaskActivity" }],
            excludeAssigneesFrom: overrideExclusions);

    [Fact]
    public void Validate_SourceInOverrideExclusions_Rejected()
        => ValidateWithExclusions(["int-pma-input"], BookVerification()).Should().Contain("excludeAssigneesFrom");

    [Fact]
    public void Validate_SourceInJsonExclusions_RejectedWhenOverrideListNull()
    {
        var activity = BookVerification();
        activity.Properties["assignmentRules"] = new Dictionary<string, object>
        {
            ["excludeAssigneesFrom"] = new List<string> { "int-pma-input" }
        };

        ValidateWithExclusions(null, activity).Should().Contain("excludeAssigneesFrom");
    }

    [Fact]
    public void Validate_OverrideExclusionsReplaceJsonOnes_Passes()
    {
        var activity = BookVerification();
        activity.Properties["assignmentRules"] = new Dictionary<string, object>
        {
            ["excludeAssigneesFrom"] = new List<string> { "int-pma-input" }
        };

        // An explicit (here empty) override list wins over the JSON list, which is what the pipeline does.
        ValidateWithExclusions([], activity).Should().BeNull();
    }

    // ── followup-selection override fields ──

    [Fact]
    public void Validate_FollowupSelection_RejectsFieldsItDoesNotRead()
    {
        var defs = new[] { FollowupSelection() };
        ValidateBoth(
                "internal-followup-selection", null, null, null, null, defs, specificAssignee: "somchai")
            .Should().Contain("only reads sameAssigneeAsActivity");
        ValidateBoth(
                "internal-followup-selection", null, null, null, null, defs, assigneeGroup: "G")
            .Should().Contain("only reads sameAssigneeAsActivity");
        ValidateBoth(
                "internal-followup-selection", null, null, null, null, defs, teamConstrained: true)
            .Should().Contain("only reads sameAssigneeAsActivity");
        ValidateBoth(
                "internal-followup-selection", null, null, null, null, defs, excludeAssigneesFrom: ["x"])
            .Should().Contain("only reads sameAssigneeAsActivity");
    }

    [Fact]
    public void ValidateBasics_RejectsNearMissKey()
        => TaskAssignmentConfigAdminEndpoints.ValidateBasics(
                "a", null, null, null, new() { ["SameAssigneeAsActivity"] = "b" })
            .Should().Contain("Unknown key");

    [Fact]
    public void Validate_SourceInExclusionsButStrategyNotUsed_Passes()
        => ValidateWithExclusions(["int-pma-input"], BookVerification(), strategy: "round_robin").Should().BeNull();

    [Fact]
    public void ValidateBasics_WorkflowDefinitionId_MustBeGuidOrEmpty()
    {
        string? Check(string? id) => TaskAssignmentConfigAdminEndpoints.ValidateBasics("a", null, null, null, null, id);

        Check(null).Should().BeNull();
        Check("").Should().BeNull();
        Check(Guid.NewGuid().ToString()).Should().BeNull();
        Check("appraisal").Should().Contain("must be a GUID");
        Check("{" + Guid.NewGuid() + "}").Should().Contain("must be a GUID");
        Check(Guid.NewGuid().ToString("N")).Should().Contain("must be a GUID");
    }

    // ── a row can always be switched off: no definition-side checks when IsActive is false ──

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_DefinitionSideChecksRunOnlyForActiveRows(bool isActive)
    {
        var service = Substitute.For<ITaskConfigurationService>();
        service.CreateConfigurationAsync(Arg.Any<CreateTaskAssignmentConfigurationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TaskAssignmentConfigurationDto { Id = Guid.NewGuid() });
        await using var db = new WorkflowDbContext(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"AdminCreate_{Guid.NewGuid()}").Options);

        await TaskAssignmentConfigAdminEndpoints.Create(
            new CreateTaskAssignmentConfigurationRequest
            {
                ActivityId = "appraisal-book-verification",
                PrimaryStrategies = ["same_assignee_as_activity"],
                IsActive = isActive
            },
            service, Substitute.For<ICurrentUserService>(), db, CancellationToken.None);

        // An active row hits the definition-side "token needs a source" check (400, not saved); an inactive one
        // skips the definition entirely and is saved.
        await service.Received(isActive ? 0 : 1).CreateConfigurationAsync(
            Arg.Any<CreateTaskAssignmentConfigurationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validate_FollowupSelection_IgnoresValuesThatWouldNotTakeEffect()
        => ValidateBoth(
                "internal-followup-selection", null, null, null, null, [FollowupSelection()],
                specificAssignee: "  ", assigneeGroup: "", teamConstrained: false, excludeAssigneesFrom: [])
            .Should().BeNull();
}
