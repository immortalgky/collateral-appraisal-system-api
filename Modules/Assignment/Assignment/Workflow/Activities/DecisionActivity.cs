using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Schema;

namespace Assignment.Workflow.Activities;

public class DecisionActivity : WorkflowActivityBase
{
    public override string ActivityType => ActivityTypes.DecisionActivity;
    public override string Name => "Decision Activity";
    public override string Description => "Routes workflow based on conditions or decision rules";

    public override async Task<ActivityResult> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var conditions = GetProperty<Dictionary<string, string>>(context, "conditions", new Dictionary<string, string>());
        var defaultRoute = GetProperty<string>(context, "defaultRoute");
        
        // Evaluate conditions to determine next activity
        foreach (var condition in conditions)
        {
            if (EvaluateCondition(context, condition.Value))
            {
                var outputData = new Dictionary<string, object>
                {
                    ["decision"] = condition.Key,
                    ["condition"] = condition.Value,
                    ["evaluatedAt"] = DateTime.UtcNow
                };

                return ActivityResult.Success(outputData, condition.Key);
            }
        }

        // If no conditions match, use default route
        if (!string.IsNullOrEmpty(defaultRoute))
        {
            var outputData = new Dictionary<string, object>
            {
                ["decision"] = "default",
                ["evaluatedAt"] = DateTime.UtcNow
            };

            return ActivityResult.Success(outputData, defaultRoute);
        }

        return ActivityResult.Failed("No matching condition found and no default route specified");
    }

    public override Task<Core.ValidationResult> ValidateAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        
        var conditions = GetProperty<Dictionary<string, string>>(context, "conditions", new Dictionary<string, string>());
        var defaultRoute = GetProperty<string>(context, "defaultRoute");
        
        if (!conditions.Any() && string.IsNullOrEmpty(defaultRoute))
        {
            errors.Add("DecisionActivity must have at least one condition or a default route");
        }

        return Task.FromResult(errors.Any() ? Core.ValidationResult.Failure(errors.ToArray()) : Core.ValidationResult.Success());
    }
}