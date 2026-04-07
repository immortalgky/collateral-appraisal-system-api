using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;

namespace Workflow.Workflow.Activities;

/// <summary>
/// Automatic routing activity that evaluates conditions to determine workflow path.
/// Routes to company-selection activity for external assignments or to admin for internal review.
/// </summary>
public class RoutingActivity : WorkflowActivityBase
{
    private readonly ILogger<RoutingActivity> _logger;

    public RoutingActivity(ILogger<RoutingActivity> logger)
    {
        _logger = logger;
    }

    public override string ActivityType => ActivityTypes.RoutingActivity;
    public override string Name => "Routing Activity";
    public override string Description => "Automatic routing based on configurable conditions";

    protected override Task<ActivityResult> ExecuteActivityAsync(
        ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        var routingConditions = GetProperty<Dictionary<string, string>>(context, "routingConditions");
        var defaultDecision = GetProperty<string>(context, "defaultDecision", "admin_review");

        var decision = defaultDecision;

        // Evaluate each routing condition against workflow variables
        if (routingConditions != null)
        {
            foreach (var (conditionName, expression) in routingConditions)
            {
                if (EvaluateCondition(context, expression))
                {
                    decision = conditionName;
                    _logger.LogInformation(
                        "RoutingActivity {ActivityId}: condition '{ConditionName}' matched (expression: {Expression})",
                        context.ActivityId, conditionName, expression);
                    break;
                }
            }
        }

        var routingPath = decision.Contains("internal") ? "internal" :
                          decision == "auto_assign_external" ? "external" : "admin";

        var outputData = new Dictionary<string, object>
        {
            ["decision"] = decision,
            ["routingDecision"] = decision,
            ["routingPath"] = routingPath,
            ["routedAt"] = DateTime.UtcNow
        };

        // For auto-assign external, set selectionMethod so CompanySelectionActivity knows to use round-robin
        if (decision == "auto_assign_external")
        {
            outputData["assignmentMethod"] = "roundrobin";
        }

        _logger.LogInformation(
            "RoutingActivity {ActivityId}: routed with decision '{Decision}'",
            context.ActivityId, decision);

        return Task.FromResult(ActivityResult.Success(outputData));
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
