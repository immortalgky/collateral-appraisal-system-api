using Workflow.AssigneeSelection.Services;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;

namespace Workflow.Workflow.Activities;

/// <summary>
/// Automatic routing activity that evaluates conditions to determine workflow path.
/// Can auto-assign to an external company via round-robin or route to internal admin.
/// </summary>
public class RoutingActivity : WorkflowActivityBase
{
    private readonly ICompanyRoundRobinService _companyRoundRobinService;
    private readonly ILogger<RoutingActivity> _logger;

    public RoutingActivity(
        ICompanyRoundRobinService companyRoundRobinService,
        ILogger<RoutingActivity> logger)
    {
        _companyRoundRobinService = companyRoundRobinService;
        _logger = logger;
    }

    public override string ActivityType => ActivityTypes.RoutingActivity;
    public override string Name => "Routing Activity";
    public override string Description => "Automatic routing based on configurable conditions";

    protected override async Task<ActivityResult> ExecuteActivityAsync(
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

        var routingPath = decision == "auto_assign_external" ? "external" : "internal";

        var outputData = new Dictionary<string, object>
        {
            ["decision"] = decision,
            ["routingPath"] = routingPath,
            ["routedAt"] = DateTime.UtcNow
        };

        // If the decision is auto-assign to external company, perform round-robin selection
        if (decision == "auto_assign_external")
        {
            var companyResult = await _companyRoundRobinService.SelectCompanyAsync(cancellationToken);

            if (companyResult.IsSuccess)
            {
                outputData["assignedCompanyId"] = companyResult.CompanyId!.Value.ToString();
                outputData["assignedCompanyName"] = companyResult.CompanyName!;

                _logger.LogInformation(
                    "RoutingActivity {ActivityId}: auto-assigned to company {CompanyName} ({CompanyId})",
                    context.ActivityId, companyResult.CompanyName, companyResult.CompanyId);
            }
            else
            {
                // Fallback to admin review if company selection fails
                _logger.LogWarning(
                    "RoutingActivity {ActivityId}: company selection failed ({Error}), falling back to admin_review",
                    context.ActivityId, companyResult.ErrorMessage);

                decision = "admin_review";
                outputData["decision"] = decision;
                outputData["routingFallbackReason"] = companyResult.ErrorMessage ?? "Company selection failed";
            }
        }

        _logger.LogInformation(
            "RoutingActivity {ActivityId}: routed with decision '{Decision}'",
            context.ActivityId, decision);

        return ActivityResult.Success(outputData);
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
