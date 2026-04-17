using System.Text.Json;
using Workflow.Workflow.Engine.Expression;
using Workflow.Workflow.Repositories;

namespace Workflow.Workflow.Features.GetActivityFormSchema;

public class GetActivityFormSchemaQueryHandler
    : IRequestHandler<GetActivityFormSchemaQuery, GetActivityFormSchemaResponse>
{
    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly IExpressionEvaluator _expressionEvaluator;

    public GetActivityFormSchemaQueryHandler(
        IWorkflowInstanceRepository instanceRepository,
        IExpressionEvaluator expressionEvaluator)
    {
        _instanceRepository = instanceRepository;
        _expressionEvaluator = expressionEvaluator;
    }

    public async Task<GetActivityFormSchemaResponse> Handle(
        GetActivityFormSchemaQuery request, CancellationToken cancellationToken)
    {
        var instance = await _instanceRepository.GetByIdAsync(request.WorkflowInstanceId, cancellationToken)
            ?? throw new InvalidOperationException($"Workflow instance {request.WorkflowInstanceId} not found");

        var definition = instance.WorkflowDefinition;
        if (definition is null || string.IsNullOrEmpty(definition.JsonDefinition))
            return new GetActivityFormSchemaResponse
            {
                ActivityId = request.ActivityId,
                ActivityName = request.ActivityId,
                ActivityType = "Unknown"
            };

        using var doc = JsonDocument.Parse(definition.JsonDefinition);
        var root = doc.RootElement;

        var activity = FindActivity(root, request.ActivityId);
        if (activity is null)
            return new GetActivityFormSchemaResponse
            {
                ActivityId = request.ActivityId,
                ActivityName = request.ActivityId,
                ActivityType = "Unknown"
            };

        var activityName = activity.Value.TryGetProperty("name", out var nameProp)
            ? nameProp.GetString() ?? request.ActivityId
            : request.ActivityId;

        var activityType = activity.Value.TryGetProperty("type", out var typeProp)
            ? typeProp.GetString() ?? "Unknown"
            : "Unknown";

        var actions = new List<ActionDto>();
        var formFields = new List<FormFieldDto>();

        if (activity.Value.TryGetProperty("properties", out var properties))
        {
            actions = ReadActions(properties, instance.Variables);
            formFields = ReadFormFields(properties);

            // Resolve targetActivityId for actions (same logic as GetActivityActions)
            var valueToConditionKey = BuildValueToConditionKeyMap(properties);
            var conditionKeyToTarget = BuildConditionKeyToTargetMap(root, request.ActivityId);

            foreach (var action in actions)
            {
                if (valueToConditionKey.TryGetValue(action.Value, out var conditionKey)
                    && conditionKeyToTarget.TryGetValue(conditionKey, out var targetId))
                {
                    action.TargetActivityId = targetId;
                }
            }
        }

        // Collect current values from workflow variables that match form field names
        var currentValues = new Dictionary<string, object?>();
        foreach (var field in formFields)
        {
            if (instance.Variables.TryGetValue(field.Name, out var value))
                currentValues[field.Name] = value;
        }

        return new GetActivityFormSchemaResponse
        {
            ActivityId = request.ActivityId,
            ActivityName = activityName,
            ActivityType = activityType,
            Actions = actions,
            FormFields = formFields,
            CurrentValues = currentValues
        };
    }

    private static JsonElement? FindActivity(JsonElement root, string activityId)
    {
        if (!root.TryGetProperty("activities", out var activities) ||
            activities.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var activity in activities.EnumerateArray())
        {
            if (activity.TryGetProperty("id", out var idProp) && idProp.GetString() == activityId)
                return activity;
        }

        return null;
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

            if (!string.IsNullOrEmpty(condition))
            {
                try
                {
                    if (!_expressionEvaluator.EvaluateExpression(condition, variables))
                        continue;
                }
                catch
                {
                    continue; // fail closed — hide action if evaluation throws
                }
            }

            if (!string.IsNullOrEmpty(value))
            {
                actions.Add(new ActionDto
                {
                    Value = value,
                    Label = label ?? value,
                    AssignmentMode = mode ?? "system"
                });
            }
        }

        return actions;
    }

    private static List<FormFieldDto> ReadFormFields(JsonElement properties)
    {
        var fields = new List<FormFieldDto>();

        if (!properties.TryGetProperty("formFields", out var fieldsArray) ||
            fieldsArray.ValueKind != JsonValueKind.Array)
            return fields;

        foreach (var item in fieldsArray.EnumerateArray())
        {
            var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
            var label = item.TryGetProperty("label", out var l) ? l.GetString() : null;
            var type = item.TryGetProperty("type", out var t) ? t.GetString() : "text";
            var required = item.TryGetProperty("required", out var r) && r.GetBoolean();
            var defaultValue = item.TryGetProperty("defaultValue", out var d) ? d.GetString() : null;

            List<string>? options = null;
            if (item.TryGetProperty("options", out var optArray) && optArray.ValueKind == JsonValueKind.Array)
            {
                options = optArray.EnumerateArray()
                    .Select(o => o.GetString() ?? "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }

            if (!string.IsNullOrEmpty(name))
            {
                fields.Add(new FormFieldDto
                {
                    Name = name,
                    Label = label ?? name,
                    Type = type ?? "text",
                    Required = required,
                    DefaultValue = defaultValue,
                    Options = options
                });
            }
        }

        return fields;
    }

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
}
