using FluentAssertions;
using Workflow.Data.Entities;
using Workflow.Services.Configuration;
using Xunit;

namespace Workflow.Tests.Workflow;

/// <summary>
/// Pins the seeded initial-routing rule set. These assertions exist because the precedence here is
/// load-bearing and was previously seeded inverted: the rules must reproduce what
/// appraisal-workflow.json's routingConditions did, with the phase-1 go-live override layered on
/// top as a single deactivatable row.
/// </summary>
public class AutoAssignmentRuleSeedTests
{
    private static readonly AutoAssignmentRule[] Rules = AutoAssignmentRuleSeeder.BuildRules();

    [Fact]
    public void AppraisalBookRule_OutranksPmaRule()
    {
        // appraisal-workflow.json lists auto_assign_internal (hasAppraisalBook) BEFORE pma_flow and
        // breaks on first match, so a request carrying BOTH flags goes to internal execution with
        // the book it already has. Seeding isPma first would silently divert those cases into
        // int-pma-input for a key-in they do not need.
        var book = Single("hasAppraisalBook == true");
        var pma = Single("isPma == true");

        book.Priority.Should().BeLessThan(pma.Priority);
        book.RoutingDecision.Should().Be(RoutingDecisions.Internal);
        pma.RoutingDecision.Should().Be(RoutingDecisions.Pma);
    }

    [Fact]
    public void PathSelectionRules_AreActive_SoTodaysBehaviourIsPreservedDuringGoLive()
    {
        // These are path selection, not auto-assignment. If they were inactive the go-live
        // catch-all would swallow PMA and appraisal-book cases too, skipping int-pma-input.
        Single("hasAppraisalBook == true").IsActive.Should().BeTrue();
        Single("isPma == true").IsActive.Should().BeTrue();
        Rules.Single(r => r.ConditionExpression?.Contains("bankingSegment == 'IBG'") == true)
            .IsActive.Should().BeTrue();
    }

    [Fact]
    public void GoLiveCatchAll_IsActive_AndOutranksTheSteadyStateCatchAll()
    {
        var goLive = CatchAll(RoutingDecisions.AdminReview);
        var steadyState = CatchAll(RoutingDecisions.ExternalRoundRobin);

        goLive.IsActive.Should().BeTrue();
        goLive.Priority.Should().BeLessThan(steadyState.Priority);
    }

    [Fact]
    public void SteadyStateCatchAll_IsSeededInactive_AndAutoAssignsExternally()
    {
        // Turned on when the go-live row is turned off. Seeded inactive so the table visibly
        // records that auto-assignment is currently suppressed.
        var steadyState = CatchAll(RoutingDecisions.ExternalRoundRobin);

        steadyState.IsActive.Should().BeFalse();
        steadyState.RoutingDecision.Should().Be(RoutingDecisions.ExternalRoundRobin);
    }

    [Fact]
    public void GoLiveCatchAll_MatchesEverything_SoNothingFallsThroughToExternal()
    {
        var goLive = CatchAll(RoutingDecisions.AdminReview);

        goLive.ConditionExpression.Should().BeNull();
        goLive.Channels.Should().BeNull();
        goLive.EntrySources.Should().BeNull();
        goLive.LoanTypes.Should().BeNull();
        goLive.Priorities.Should().BeNull();
        goLive.MinFacilityLimit.Should().BeNull();
        goLive.MaxFacilityLimit.Should().BeNull();
    }

    [Fact]
    public void EveryRule_CarriesAKnownRoutingDecision_AndAUniquePriority()
    {
        Rules.Should().OnlyContain(r => RoutingDecisions.IsValid(r.RoutingDecision));
        Rules.Select(r => r.Priority).Should().OnlyHaveUniqueItems();
    }

    private static AutoAssignmentRule Single(string conditionExpression) =>
        Rules.Single(r => r.ConditionExpression == conditionExpression);

    /// <summary>A catch-all carries no conditions at all, so it matches every case.</summary>
    private static AutoAssignmentRule CatchAll(string routingDecision) =>
        Rules.Single(r => r.RoutingDecision == routingDecision && r.ConditionExpression is null);
}
