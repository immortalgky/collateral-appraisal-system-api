using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Workflow.Workflow.Pipeline.Steps;

/// <summary>
/// Sets a workflow variable from step parameters.
/// Parameters JSON: {"variable": "assignmentType", "value": "Internal"}
/// </summary>
public class SetVariableStep(ILogger<SetVariableStep> logger) : IActivityProcessStep
{
    public string Name => "SetVariable";

    public Task<ProcessStepResult> ExecuteAsync(ProcessStepContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context.Parameters))
            return Task.FromResult(ProcessStepResult.Fail("Missing parameters for SetVariable step"));

        try
        {
            using var doc = JsonDocument.Parse(context.Parameters);
            var root = doc.RootElement;

            var variable = root.TryGetProperty("variable", out var v) ? v.GetString() : null;
            var value = root.TryGetProperty("value", out var val) ? val.GetString() : null;

            if (string.IsNullOrEmpty(variable) || value is null)
                return Task.FromResult(ProcessStepResult.Fail("Missing 'variable' or 'value' in parameters"));

            if (context.Variables is not null)
            {
                context.Variables[variable] = value;
                logger.LogInformation(
                    "SetVariableStep: set {Variable} = {Value} for activity {ActivityName}",
                    variable, value, context.ActivityName);
            }

            return Task.FromResult(ProcessStepResult.Ok());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SetVariableStep failed for activity {ActivityName}", context.ActivityName);
            return Task.FromResult(ProcessStepResult.Fail(ex.Message));
        }
    }
}
