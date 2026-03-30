using System.Text.Json;
using Workflow.AssigneeSelection.Core;

namespace Workflow.AssigneeSelection.Strategies;

/// <summary>
/// Assigns tasks to a user specified in a named workflow variable.
/// Reads the variable name from Properties["assigneeVariable"], then resolves
/// the actual username from Variables[variableName].
/// </summary>
public class VariableAssigneeSelector : IAssigneeSelector
{
    private readonly ILogger<VariableAssigneeSelector> _logger;

    public VariableAssigneeSelector(ILogger<VariableAssigneeSelector> logger)
    {
        _logger = logger;
    }

    public Task<AssigneeSelectionResult> SelectAssigneeAsync(
        AssignmentContext context,
        CancellationToken cancellationToken = default)
    {
        var variableName = GetStringValue(context.Properties, "assigneeVariable");

        if (string.IsNullOrWhiteSpace(variableName))
        {
            _logger.LogInformation(
                "VariableAssignee selector skipped for activity {ActivityName}: no assigneeVariable configured",
                context.ActivityName);

            return Task.FromResult(
                AssigneeSelectionResult.Failure("VariableAssignee strategy requires 'assigneeVariable' in properties"));
        }

        var username = GetStringValue(context.Variables, variableName);

        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogInformation(
                "VariableAssignee selector skipped for activity {ActivityName}: variable '{VariableName}' is not set",
                context.ActivityName, variableName);

            return Task.FromResult(
                AssigneeSelectionResult.Failure($"Workflow variable '{variableName}' is not set or empty"));
        }

        _logger.LogInformation(
            "VariableAssignee selector assigned user {Username} for activity {ActivityName} from variable {VariableName}",
            username, context.ActivityName, variableName);

        return Task.FromResult(
            AssigneeSelectionResult.Success(username, new Dictionary<string, object>
            {
                ["SelectionStrategy"] = "VariableAssignee",
                ["SourceVariable"] = variableName,
                ["ResolvedAssignee"] = username
            }));
    }

    private static string? GetStringValue(Dictionary<string, object>? dict, string key)
    {
        if (dict is null || !dict.TryGetValue(key, out var val))
            return null;

        if (val is string s) return s;
        if (val is JsonElement { ValueKind: JsonValueKind.String } je) return je.GetString();
        return val?.ToString();
    }
}
