using Workflow.Workflow.Engine.Expression;
using Workflow.Workflow.Repositories;

namespace Workflow.Workflow.Features.GetActivityActions;

public class GetActivityActionsQueryHandler
    : IRequestHandler<GetActivityActionsQuery, GetActivityActionsResponse>
{
    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly IExpressionEvaluator _expressionEvaluator;

    public GetActivityActionsQueryHandler(
        IWorkflowInstanceRepository instanceRepository,
        IExpressionEvaluator expressionEvaluator)
    {
        _instanceRepository = instanceRepository;
        _expressionEvaluator = expressionEvaluator;
    }

    public async Task<GetActivityActionsResponse> Handle(
        GetActivityActionsQuery request, CancellationToken cancellationToken)
    {
        var instance = await _instanceRepository.GetByIdAsync(request.WorkflowInstanceId, cancellationToken)
                       ?? throw new InvalidOperationException(
                           $"Workflow instance {request.WorkflowInstanceId} not found");

        var definition = instance.WorkflowDefinition;
        if (definition is null || string.IsNullOrEmpty(definition.JsonDefinition))
            return new GetActivityActionsResponse
            {
                ActivityId = request.ActivityId,
                ActivityName = request.ActivityId
            };

        using var doc = JsonDocument.Parse(definition.JsonDefinition);
        var root = doc.RootElement;

        var activity = FindActivity(root, request.ActivityId);
        if (activity is null)
            return new GetActivityActionsResponse
            {
                ActivityId = request.ActivityId,
                ActivityName = request.ActivityId
            };

        var activityName = activity.Value.TryGetProperty("name", out var nameProp)
            ? nameProp.GetString() ?? request.ActivityId
            : request.ActivityId;

        if (!activity.Value.TryGetProperty("properties", out var properties))
            return new GetActivityActionsResponse
            {
                ActivityId = request.ActivityId,
                ActivityName = activityName
            };

        var canRaiseFollowup = properties.TryGetProperty("canRaiseFollowup", out var crf) &&
                               (crf.ValueKind == JsonValueKind.True ||
                                (crf.ValueKind == JsonValueKind.String &&
                                 bool.TryParse(crf.GetString(), out var b) && b));

        // Read actions array from properties, or voteOptions for ApprovalActivity
        var actions = ReadActions(properties, instance.Variables);
        if (actions.Count == 0)
            actions = ReadVoteOptions(properties);

        // Read decisionConditions to map value → condition key
        var valueToConditionKey = BuildValueToConditionKeyMap(properties);

        // Read transitions to map condition key → target activity
        var conditionKeyToTarget = BuildConditionKeyToTargetMap(root, request.ActivityId);

        // Resolve targetActivityId for each action
        foreach (var action in actions)
            if (valueToConditionKey.TryGetValue(action.Value, out var conditionKey)
                && conditionKeyToTarget.TryGetValue(conditionKey, out var targetId))
                action.TargetActivityId = targetId;

        return new GetActivityActionsResponse
        {
            ActivityId = request.ActivityId,
            ActivityName = activityName,
            Actions = actions,
            CanRaiseFollowup = canRaiseFollowup
        };
    }

    private static JsonElement? FindActivity(JsonElement root, string activityId)
    {
        if (!root.TryGetProperty("activities", out var activities) ||
            activities.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var activity in activities.EnumerateArray())
            if (activity.TryGetProperty("id", out var idProp) && idProp.GetString() == activityId)
                return activity;

        return null;
    }

    private static List<ActionDto> ReadVoteOptions(JsonElement properties)
    {
        var actions = new List<ActionDto>();

        if (!properties.TryGetProperty("voteOptions", out var voteOptions) ||
            voteOptions.ValueKind != JsonValueKind.Array)
            return actions;

        // voteMovements is a { "vote_value": "F|B|C" } map used by ApprovalActivity at
        // resolve-time; surface it here so the UI can style Cancel/Backward buttons.
        var voteMovements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (properties.TryGetProperty("voteMovements", out var vm) && vm.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in vm.EnumerateObject())
                if (prop.Value.ValueKind == JsonValueKind.String)
                    voteMovements[prop.Name] = NormalizeMovement(prop.Value.GetString());
        }

        foreach (var item in voteOptions.EnumerateArray())
        {
            var value = item.GetString();
            if (!string.IsNullOrEmpty(value))
                actions.Add(new ActionDto
                {
                    Value = value,
                    Label = value.Replace("_", " "),
                    AssignmentMode = "system",
                    Movement = voteMovements.TryGetValue(value, out var m) ? m : "F"
                });
        }

        return actions;
    }

    private List<ActionDto> ReadActions(JsonElement properties, Dictionary<string, object> variables)
    {
        var actions = new List<ActionDto>();

        if (!properties.TryGetProperty("actions", out var actionsArray) ||
            actionsArray.ValueKind != JsonValueKind.Array)
            return actions;

        foreach (var item in actionsArray.EnumerateArray())
        {
            var value = item.TryGetProperty("value", out var v) ? v.GetString() : null;
            var label = item.TryGetProperty("label", out var l) ? l.GetString() : null;
            var mode = item.TryGetProperty("assignmentMode", out var m) ? m.GetString() : "system";
            var condition = item.TryGetProperty("condition", out var cond) ? cond.GetString() : null;
            var rawMovement = item.TryGetProperty("movement", out var mv) ? mv.GetString() : null;
            var movement = NormalizeMovement(rawMovement);

            if (!string.IsNullOrEmpty(condition))
                try
                {
                    if (!_expressionEvaluator.EvaluateExpression(condition, variables))
                        continue;
                }
                catch
                {
                    continue; // fail closed — hide action if evaluation throws
                }

            if (!string.IsNullOrEmpty(value) && value != "EXT" && value != "INT")
                actions.Add(new ActionDto
                {
                    Value = value,
                    Label = label ?? value,
                    AssignmentMode = mode ?? "system",
                    Movement = movement
                });
        }

        return actions;
    }

    /// <summary>
    /// Maps action values (e.g., "EXT_RR") to their condition keys (e.g., "route_external_roundrobin")
    /// by parsing the condition expressions like "admin_decisionTaken == 'EXT_RR'".
    /// </summary>
    private static Dictionary<string, string> BuildValueToConditionKeyMap(JsonElement properties)
    {
        var map = new Dictionary<string, string>();

        if (!properties.TryGetProperty("decisionConditions", out var conditions) ||
            conditions.ValueKind != JsonValueKind.Object)
            return map;

        foreach (var condition in conditions.EnumerateObject())
        {
            var expression = condition.Value.GetString();
            if (string.IsNullOrEmpty(expression)) continue;

            // Extract value from expressions like "admin_decisionTaken == 'EXT_RR'"
            var quoteStart = expression.IndexOf('\'');
            var quoteEnd = expression.LastIndexOf('\'');
            if (quoteStart >= 0 && quoteEnd > quoteStart)
            {
                var decisionValue = expression.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
                map[decisionValue] = condition.Name;
            }
        }

        return map;
    }

    /// <summary>
    /// Maps condition keys (e.g., "route_external_roundrobin") to target activity IDs
    /// by matching transition conditions like "decision == 'route_external_roundrobin'".
    /// </summary>
    private static Dictionary<string, string> BuildConditionKeyToTargetMap(JsonElement root, string fromActivityId)
    {
        var map = new Dictionary<string, string>();

        if (!root.TryGetProperty("transitions", out var transitions) ||
            transitions.ValueKind != JsonValueKind.Array)
            return map;

        foreach (var transition in transitions.EnumerateArray())
        {
            var from = transition.TryGetProperty("from", out var f) ? f.GetString() : null;
            if (from != fromActivityId) continue;

            var to = transition.TryGetProperty("to", out var t) ? t.GetString() : null;
            var condition = transition.TryGetProperty("condition", out var c) ? c.GetString() : null;

            if (string.IsNullOrEmpty(to) || string.IsNullOrEmpty(condition)) continue;

            // Extract key from "decision == 'key'" pattern
            var quoteStart = condition.IndexOf('\'');
            var quoteEnd = condition.LastIndexOf('\'');
            if (quoteStart >= 0 && quoteEnd > quoteStart)
            {
                var conditionKey = condition.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
                map[conditionKey] = to;
            }
        }

        return map;
    }

    /// <summary>
    /// Normalises a raw movement string from workflow JSON into one of the allowed single
    /// letters: F (forward), B (backward), C (cancel). Any unrecognised or missing value
    /// falls back to "F" so existing behaviour is preserved.
    /// </summary>
    private static string NormalizeMovement(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "F";

        var upper = raw.Trim().ToUpperInvariant();
        return upper is "F" or "B" or "C" ? upper : "F";
    }
}