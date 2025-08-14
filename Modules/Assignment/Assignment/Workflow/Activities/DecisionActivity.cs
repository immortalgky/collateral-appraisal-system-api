using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Schema;

namespace Assignment.Workflow.Activities;

public class DecisionActivity : WorkflowActivityBase
{
    public override string ActivityType => ActivityTypes.DecisionActivity;
    public override string Name => "Decision Activity";
    public override string Description => "Routes workflow based on conditions or decision rules";

    public override async Task<ActivityResult> ExecuteAsync(ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        var conditions =
            GetProperty<Dictionary<string, string>>(context, "conditions", new Dictionary<string, string>());
        var defaultDecision = GetProperty<string>(context, "defaultDecision", "default");

        // Evaluate conditions to determine decision result
        foreach (var condition in conditions)
        {
            if (EvaluateCondition(context, condition.Value))
            {
                var outputData = new Dictionary<string, object>
                {
                    ["decision"] = condition.Key,
                    ["condition"] = condition.Value,
                    ["evaluatedAt"] = DateTime.Now
                };

                return ActivityResult.Success(outputData);
            }
        }

        // If no conditions match, use the default decision
        var defaultOutputData = new Dictionary<string, object>
        {
            ["decision"] = defaultDecision,
            ["evaluatedAt"] = DateTime.Now
        };

        return ActivityResult.Success(defaultOutputData);
    }

    public override Task<Core.ValidationResult> ValidateAsync(ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        var conditions =
            GetProperty<Dictionary<string, string>>(context, "conditions", new Dictionary<string, string>());

        if (!conditions.Any())
        {
            errors.Add("DecisionActivity must have at least one condition");
        }

        // Validate condition expressions
        foreach (var condition in conditions)
        {
            if (string.IsNullOrWhiteSpace(condition.Value))
            {
                errors.Add($"Condition for decision '{condition.Key}' cannot be empty");
            }
        }

        return Task.FromResult(errors.Any()
            ? Core.ValidationResult.Failure(errors.ToArray())
            : Core.ValidationResult.Success());
    }
}