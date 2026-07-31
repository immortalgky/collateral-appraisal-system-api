using Workflow.Data.Entities;
using Workflow.Services.Configuration;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;

namespace Workflow.Workflow.Activities;

/// <summary>
/// Automatic routing activity that determines the workflow path.
/// Routes to company-selection activity for external assignments or to admin for internal review.
///
/// The decision comes from the admin-configurable workflow.AutoAssignmentRules table when a rule
/// matches; otherwise it falls back to the routingConditions / defaultDecision declared on the
/// activity in appraisal-workflow.json. That fallback is what makes the rule table safe to deploy
/// empty — behaviour is then byte-for-byte what it was before the table existed.
/// </summary>
public class RoutingActivity : WorkflowActivityBase
{
    private readonly IAutoAssignmentRuleService _ruleService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<RoutingActivity> _logger;

    public RoutingActivity(
        IAutoAssignmentRuleService ruleService,
        IDateTimeProvider dateTimeProvider,
        ILogger<RoutingActivity> logger)
    {
        _ruleService = ruleService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Maps a rule's RoutingDecision onto the decision string the outgoing transitions expect.
    /// Keep in sync with the transition conditions on initial-routing in appraisal-workflow.json.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> DecisionByRoutingDecision =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [RoutingDecisions.AdminReview] = "admin_review",
            [RoutingDecisions.Internal] = "auto_assign_internal",
            [RoutingDecisions.ExternalRoundRobin] = "auto_assign_external",
            [RoutingDecisions.Pma] = "pma_flow"
        };

    public override string ActivityType => ActivityTypes.RoutingActivity;
    public override string Name => "Routing Activity";
    public override string Description => "Automatic routing based on configurable conditions";

    protected override async Task<ActivityResult> ExecuteActivityAsync(
        ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        // Replay guard: routing is a one-time decision for the workflow lifetime.
        // Any re-execution (route-back or otherwise) reuses the original decision so the
        // workflow cannot switch branches mid-lifecycle.
        var existingDecision = GetVariable<string>(context, "routingDecision", "");
        if (!string.IsNullOrEmpty(existingDecision))
        {
            var replayOutput = new Dictionary<string, object>
            {
                ["decision"] = existingDecision,
                ["routingDecision"] = existingDecision,
                ["routingPath"] = GetVariable<string>(context, "routingPath", "")
            };
            if (existingDecision == "auto_assign_external")
                replayOutput["assignmentMethod"] = GetVariable<string>(context, "assignmentMethod", "roundrobin");

            _logger.LogInformation(
                "RoutingActivity {ActivityId}: replaying — reusing previous decision '{Decision}'",
                context.ActivityId, existingDecision);

            return ActivityResult.Success(replayOutput);
        }

        var decision = await ResolveFromRulesAsync(context, cancellationToken)
                       ?? ResolveFromDefinition(context);

        var routingPath = decision.Contains("internal") ? "internal" :
                          decision == "auto_assign_external" ? "external" : "admin";

        var outputData = new Dictionary<string, object>
        {
            ["decision"] = decision,
            ["routingDecision"] = decision,
            ["routingPath"] = routingPath,
            ["routedAt"] = _dateTimeProvider.ApplicationNow
        };

        // For auto-assign external, set selectionMethod so CompanySelectionActivity knows to use round-robin
        if (decision == "auto_assign_external")
        {
            outputData["assignmentMethod"] = "roundrobin";
        }

        _logger.LogInformation(
            "RoutingActivity {ActivityId}: routed with decision '{Decision}'",
            context.ActivityId, decision);

        return ActivityResult.Success(outputData);
    }

    /// <summary>
    /// Asks the AutoAssignmentRules table for a decision. Returns null when the table is empty,
    /// nothing matched, or the lookup failed — all of which mean "use the workflow definition".
    /// A failure here must never block routing: the definition's own conditions are a complete,
    /// always-available answer, so a database hiccup degrades to the previous behaviour rather
    /// than stalling the workflow.
    /// </summary>
    private async Task<string?> ResolveFromRulesAsync(ActivityContext context, CancellationToken ct)
    {
        AutoAssignmentRule? rule;
        try
        {
            rule = await _ruleService.FindMatchingRuleAsync(context.Variables, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "RoutingActivity {ActivityId}: AutoAssignmentRules lookup failed; falling back to the workflow definition",
                context.ActivityId);
            return null;
        }

        if (rule is null) return null;

        if (!DecisionByRoutingDecision.TryGetValue(rule.RoutingDecision, out var decision))
        {
            _logger.LogWarning(
                "RoutingActivity {ActivityId}: rule '{RuleName}' carries unmapped RoutingDecision '{Value}'; falling back to the workflow definition",
                context.ActivityId, rule.RuleName, rule.RoutingDecision);
            return null;
        }

        _logger.LogInformation(
            "RoutingActivity {ActivityId}: AutoAssignmentRule '{RuleName}' → decision '{Decision}'",
            context.ActivityId, rule.RuleName, decision);

        return decision;
    }

    /// <summary>
    /// The pre-rule-table behaviour: first matching routingConditions entry wins, else defaultDecision.
    /// </summary>
    private string ResolveFromDefinition(ActivityContext context)
    {
        var routingConditions = GetProperty<Dictionary<string, string>>(context, "routingConditions");
        var defaultDecision = GetProperty<string>(context, "defaultDecision", "admin_review");

        if (routingConditions is not null)
        {
            foreach (var (conditionName, expression) in routingConditions)
            {
                if (EvaluateCondition(context, expression))
                {
                    _logger.LogInformation(
                        "RoutingActivity {ActivityId}: condition '{ConditionName}' matched (expression: {Expression})",
                        context.ActivityId, conditionName, expression);
                    return conditionName;
                }
            }
        }

        return defaultDecision;
    }

    protected override WorkflowActivityExecution CreateActivityExecution(ActivityContext context)
    {
        return WorkflowActivityExecution.Create(
            context.WorkflowInstance.Id,
            context.ActivityId,
            Name,
            ActivityType,
            "SYSTEM",
            context.Variables);
    }
}
