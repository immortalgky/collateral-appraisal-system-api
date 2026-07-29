using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Time;
using Workflow.Data.Entities;
using Workflow.Services.Configuration;
using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Xunit;

namespace Workflow.Tests.Workflow;

public class RoutingActivityTests
{
    private readonly IAutoAssignmentRuleService _ruleService;
    private readonly RoutingActivity _sut;

    public RoutingActivityTests()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(new DateTime(2026, 4, 19, 12, 0, 0));
        dateTimeProvider.Now.Returns(new DateTime(2026, 4, 19, 12, 0, 0));
        var logger = Substitute.For<ILogger<RoutingActivity>>();

        // Returns null unless a test says otherwise — i.e. "no rule matched", which is what makes
        // every existing test below exercise the workflow-definition fallback.
        _ruleService = Substitute.For<IAutoAssignmentRuleService>();

        _sut = new RoutingActivity(_ruleService, dateTimeProvider, logger);
    }

    private static ActivityContext CreateContext(
        Dictionary<string, object>? properties = null,
        Dictionary<string, object>? variables = null)
    {
        var workflowInstance = WorkflowInstance.Create(
            Guid.NewGuid(),
            "test-workflow",
            null,
            "test-user");

        return new ActivityContext
        {
            WorkflowInstanceId = workflowInstance.Id,
            ActivityId = "initial-routing",
            Properties = properties ?? new Dictionary<string, object>(),
            Variables = variables ?? new Dictionary<string, object>(),
            WorkflowInstance = workflowInstance
        };
    }

    [Fact]
    public async Task ExecuteAsync_AutoAssignExternal_SetsRoutingPathToExternal()
    {
        // Arrange — default decision is auto_assign_external
        var context = CreateContext(
            properties: new Dictionary<string, object>
            {
                ["defaultDecision"] = "auto_assign_external"
            },
            variables: new Dictionary<string, object>
            {
                ["amount"] = 10000
            });

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData.Should().ContainKey("routingPath");
        result.OutputData["routingPath"].Should().Be("external");
        result.OutputData["decision"].Should().Be("auto_assign_external");
        result.OutputData["assignmentMethod"].Should().Be("roundrobin");
    }

    [Fact]
    public async Task ExecuteAsync_AdminReview_SetsRoutingPathToAdmin()
    {
        // Arrange — amount > 50000 triggers admin_review condition
        var context = CreateContext(
            properties: new Dictionary<string, object>
            {
                ["routingConditions"] = new Dictionary<string, string>
                {
                    ["admin_review"] = "amount > 50000"
                },
                ["defaultDecision"] = "auto_assign_external"
            },
            variables: new Dictionary<string, object>
            {
                ["amount"] = 100000
            });

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert — only "auto_assign_internal" maps to the internal path; admin_review is its own
        // path, and the routingPath value is what the monitoring/progress screens group on.
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData.Should().ContainKey("routingPath");
        result.OutputData["routingPath"].Should().Be("admin");
        result.OutputData["decision"].Should().Be("admin_review");
    }

    [Fact]
    public async Task ExecuteAsync_AutoAssignInternal_SetsRoutingPathToInternal()
    {
        // Arrange — hasAppraisalBook routes to the in-house appraiser
        var context = CreateContext(
            properties: new Dictionary<string, object>
            {
                ["routingConditions"] = new Dictionary<string, string>
                {
                    ["auto_assign_internal"] = "hasAppraisalBook == true"
                },
                ["defaultDecision"] = "auto_assign_external"
            },
            variables: new Dictionary<string, object>
            {
                ["hasAppraisalBook"] = true
            });

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData["routingPath"].Should().Be("internal");
        result.OutputData["decision"].Should().Be("auto_assign_internal");
    }

    [Fact]
    public async Task ExecuteAsync_AutoAssignExternal_SetsAssignmentMethodToRoundRobin()
    {
        // Arrange — auto_assign_external must set the variable CompanySelectionActivity reads to
        // decide it should round-robin. That variable is "assignmentMethod"
        // (CompanySelectionActivity.cs: GetVariable<string>(context, "assignmentMethod", ...)),
        // NOT "selectionMethod" — selectionMethod is an OUTPUT the activity writes back afterwards.
        var context = CreateContext(
            properties: new Dictionary<string, object>
            {
                ["defaultDecision"] = "auto_assign_external"
            },
            variables: new Dictionary<string, object>
            {
                ["amount"] = 10000
            });

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData.Should().ContainKey("assignmentMethod");
        result.OutputData["assignmentMethod"].Should().Be("roundrobin");
        result.OutputData.Should().NotContainKey("selectionMethod");
    }

    [Fact]
    public async Task ExecuteAsync_OutputDataAlwaysContainsRoutingPath()
    {
        // Arrange — no conditions, just default
        var context = CreateContext(
            properties: new Dictionary<string, object>
            {
                ["defaultDecision"] = "admin_review"
            });

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.OutputData.Should().ContainKey("routingPath");
        result.OutputData.Should().ContainKey("decision");
        result.OutputData.Should().ContainKey("routedAt");
    }

    // ── AutoAssignmentRules integration ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_MatchingRule_OverridesTheWorkflowDefinitionDefault()
    {
        // Arrange — the definition would auto-assign externally; the rule says admin review.
        // This is the phase-1 go-live behaviour: no case auto-assigns to an external company.
        GivenMatchingRule(RoutingDecisions.AdminReview);

        var context = CreateContext(
            properties: new Dictionary<string, object>
            {
                ["defaultDecision"] = "auto_assign_external"
            });

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.OutputData["decision"].Should().Be("admin_review");
        result.OutputData["routingPath"].Should().Be("admin");
    }

    [Theory]
    [InlineData(RoutingDecisions.AdminReview, "admin_review")]
    [InlineData(RoutingDecisions.Internal, "auto_assign_internal")]
    [InlineData(RoutingDecisions.ExternalRoundRobin, "auto_assign_external")]
    [InlineData(RoutingDecisions.Pma, "pma_flow")]
    public async Task ExecuteAsync_MapsEveryRoutingDecisionOntoATransitionDecision(
        string routingDecision, string expectedDecision)
    {
        // Every RoutingDecisions value must map to a decision string the initial-routing
        // transitions actually match on, or the workflow would stall with nowhere to go.
        GivenMatchingRule(routingDecision);

        var result = await _sut.ExecuteAsync(CreateContext());

        result.OutputData["decision"].Should().Be(expectedDecision);
    }

    [Fact]
    public async Task ExecuteAsync_RuleLookupThrows_FallsBackToTheWorkflowDefinition()
    {
        // A database hiccup must degrade to the previous behaviour, never stall routing.
        _ruleService
            .FindMatchingRuleAsync(Arg.Any<IReadOnlyDictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns<Task<AutoAssignmentRule?>>(_ => throw new InvalidOperationException("db down"));

        var context = CreateContext(
            properties: new Dictionary<string, object>
            {
                ["defaultDecision"] = "auto_assign_external"
            });

        var result = await _sut.ExecuteAsync(context);

        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData["decision"].Should().Be("auto_assign_external");
    }

    [Fact]
    public async Task ExecuteAsync_NoRuleMatches_UsesTheWorkflowDefinitionConditions()
    {
        // An empty / fully-deactivated rule table must reproduce pre-rule-table behaviour exactly.
        var context = CreateContext(
            properties: new Dictionary<string, object>
            {
                ["routingConditions"] = new Dictionary<string, string>
                {
                    ["auto_assign_internal"] = "hasAppraisalBook == true"
                },
                ["defaultDecision"] = "auto_assign_external"
            },
            variables: new Dictionary<string, object>
            {
                ["hasAppraisalBook"] = true
            });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["decision"].Should().Be("auto_assign_internal");
        result.OutputData["routingPath"].Should().Be("internal");
    }

    private void GivenMatchingRule(string routingDecision)
    {
        var rule = AutoAssignmentRule.Create(
            ruleName: $"test-{routingDecision}",
            priority: 10,
            routingDecision: routingDecision,
            createdBy: "test");

        _ruleService
            .FindMatchingRuleAsync(Arg.Any<IReadOnlyDictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(rule);
    }
}
