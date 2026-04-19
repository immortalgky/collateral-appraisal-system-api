using Microsoft.Extensions.Logging;
using Workflow.Data.Entities;

namespace Workflow.Workflow.Pipeline.Steps;

/// <summary>
/// Sets a workflow variable to a fixed value from step parameters.
/// </summary>
public class SetVariableStep(ILogger<SetVariableStep> logger) : IActivityProcessStep
{
    public sealed record Parameters
    {
        /// <summary>Name of the workflow variable to set.</summary>
        public string Variable { get; init; } = default!;

        /// <summary>Value to assign.</summary>
        public string? Value { get; init; }
    }

    public StepDescriptor Descriptor { get; } = StepDescriptor.For<Parameters>(
        name: "SetVariable",
        displayName: "Set Workflow Variable",
        kind: StepKind.Action,
        description: "Sets a workflow variable to a fixed string value.");

    public Task<ProcessStepResult> ExecuteAsync(ProcessStepContext ctx, CancellationToken ct)
    {
        var p = ctx.GetParameters<Parameters>();

        if (string.IsNullOrWhiteSpace(p.Variable))
            return Task.FromResult(ProcessStepResult.Fail(
                "MISSING_VARIABLE_NAME", "Missing 'variable' in step parameters"));

        if (p.Value is null)
            return Task.FromResult(ProcessStepResult.Fail(
                "MISSING_VALUE", "Missing 'value' in step parameters"));

        // B5: Use the explicit SetVariable API instead of casting Variables to IDictionary.
        // The pipeline will merge PendingVariableWrites into the WorkflowInstance after
        // all Actions succeed, inside the outer transaction.
        ctx.SetVariable(p.Variable, p.Value);
        logger.LogInformation(
            "SetVariableStep: queued {Variable} = {Value} for activity {ActivityName}",
            p.Variable, p.Value, ctx.ActivityName);

        return Task.FromResult(ProcessStepResult.Pass());
    }
}
