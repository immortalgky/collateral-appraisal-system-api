using Workflow.Workflow.Actions.Core;
using Workflow.Workflow.Activities.Core;

namespace Workflow.Workflow.Actions;

/// <summary>
/// Action that sets workflow variables during activity execution
/// Useful for updating workflow state, calculations, and conditional branching
/// </summary>
public class SetWorkflowVariableAction : WorkflowActionBase
{
    public SetWorkflowVariableAction(ILogger<SetWorkflowVariableAction> logger) : base(logger)
    {
    }

    public override string ActionType => "SetWorkflowVariable";
    public override string Name => "Set Workflow Variable";
    public override string Description => "Sets or updates workflow variables with static values or expressions";

    protected override Task<ActionExecutionResult> ExecuteActionAsync(
        ActivityContext context,
        Dictionary<string, object> actionParameters,
        CancellationToken cancellationToken = default)
    {
        var variableName = GetParameter<string>(actionParameters, "variableName");
        var value = actionParameters.GetValueOrDefault("value");
        var expression = GetParameter<string>(actionParameters, "expression");
        var append = GetParameter<bool>(actionParameters, "append", false);

        Logger.LogDebug("Setting workflow variable '{VariableName}' for activity {ActivityId}",
            variableName, context.ActivityId);

        try
        {
            object? finalValue;

            // Determine the value to set
            if (!string.IsNullOrEmpty(expression))
            {
                // Use expression to calculate value
                finalValue = ResolveVariableExpressions(expression, context);
                Logger.LogDebug("Resolved expression '{Expression}' to value '{Value}'", expression, finalValue);
            }
            else
            {
                finalValue = value;
            }

            // Handle append operation for string values
            if (append && context.Variables.TryGetValue(variableName, out var existingValue))
            {
                var existingStr = existingValue?.ToString() ?? "";
                var newStr = finalValue?.ToString() ?? "";
                finalValue = existingStr + newStr;
                
                Logger.LogDebug("Appending to existing variable '{VariableName}': '{ExistingValue}' + '{NewValue}' = '{FinalValue}'",
                    variableName, existingStr, newStr, finalValue);
            }

            // Set the variable in the workflow context
            context.Variables[variableName] = finalValue;

            var resultMessage = $"Set variable '{variableName}' to '{finalValue}'";
            var outputData = new Dictionary<string, object>
            {
                ["variableName"] = variableName,
                ["value"] = finalValue ?? "",
                ["previousValue"] = context.Variables.GetValueOrDefault(variableName, ""),
                ["operation"] = append ? "append" : "set"
            };

            Logger.LogInformation("Successfully set workflow variable '{VariableName}' to '{Value}' for activity {ActivityId}",
                variableName, finalValue, context.ActivityId);

            return Task.FromResult(ActionExecutionResult.Success(resultMessage, outputData));
        }
        catch (Exception ex)
        {
            var errorMessage = $"Failed to set workflow variable '{variableName}': {ex.Message}";
            Logger.LogError(ex, "Error setting workflow variable '{VariableName}' for activity {ActivityId}",
                variableName, context.ActivityId);
            
            return Task.FromResult(ActionExecutionResult.Failed(errorMessage));
        }
    }

    public override Task<ActionValidationResult> ValidateAsync(
        Dictionary<string, object> actionParameters,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate required parameters
        ValidateRequiredParameter(actionParameters, "variableName", errors);

        var variableName = GetParameter<string>(actionParameters, "variableName");
        var value = actionParameters.GetValueOrDefault("value");
        var expression = GetParameter<string>(actionParameters, "expression");

        // Must have either value or expression
        if (value == null && string.IsNullOrEmpty(expression))
        {
            errors.Add("Either 'value' or 'expression' parameter must be provided");
        }

        // Both value and expression provided - warn about precedence
        if (value != null && !string.IsNullOrEmpty(expression))
        {
            warnings.Add("Both 'value' and 'expression' provided. Expression will take precedence over value.");
        }

        // Validate variable name format
        if (!string.IsNullOrEmpty(variableName))
        {
            if (variableName.Contains(" ") || variableName.Contains(".") && !variableName.StartsWith("workflow."))
            {
                warnings.Add($"Variable name '{variableName}' contains spaces or dots. Consider using camelCase naming convention.");
            }

            // Check for reserved variable names
            var reservedNames = new[] { "workflow.instanceId", "workflow.activityId", "workflow.assignee" };
            if (reservedNames.Contains(variableName))
            {
                errors.Add($"Variable name '{variableName}' is reserved and cannot be modified");
            }
        }

        return Task.FromResult(errors.Any() ? 
            ActionValidationResult.Invalid(errors, warnings) : 
            ActionValidationResult.Valid(warnings));
    }
}