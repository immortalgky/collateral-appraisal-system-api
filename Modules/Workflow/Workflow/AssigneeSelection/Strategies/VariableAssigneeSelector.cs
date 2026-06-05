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

        // Read the optional assignedType from a second workflow variable (assignedTypeVariable).
        // When assignedType == "2" the assigneeId is a group/pool name, not a personal username.
        // Setting AssignedType in metadata causes TaskActivity to emit PendingTask.AssignedType="2"
        // so the task is visible in GetPoolTasks (which filters on AssignedType='2' AND AssigneeUserId IN groups).
        var assignedTypeVarName = GetStringValue(context.Properties, "assignedTypeVariable");
        var assignedType = "1"; // default: direct user
        if (!string.IsNullOrWhiteSpace(assignedTypeVarName))
        {
            var typeFromVar = GetStringValue(context.Variables, assignedTypeVarName);
            if (!string.IsNullOrWhiteSpace(typeFromVar))
                assignedType = typeFromVar;
        }

        _logger.LogInformation(
            "VariableAssignee selector assigned {Assignee} (type={AssignedType}) for activity {ActivityName} from variable {VariableName}",
            username, assignedType, context.ActivityName, variableName);

        return Task.FromResult(
            AssigneeSelectionResult.Success(username, new Dictionary<string, object>
            {
                ["SelectionStrategy"] = "VariableAssignee",
                ["SourceVariable"] = variableName,
                ["ResolvedAssignee"] = username,
                ["AssignedType"] = assignedType
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
