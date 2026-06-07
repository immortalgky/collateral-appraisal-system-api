using Workflow.Data.Entities;

namespace Workflow.Workflow.Pipeline;

/// <summary>Describes a single step failure surfaced in the pipeline result.</summary>
/// <param name="StepName">The step's ProcessorName (stable key).</param>
/// <param name="ErrorCode">Machine code from the step (e.g. APPRAISAL_FIELDS_INVALID).</param>
/// <param name="Message">Human-readable failure message.</param>
/// <param name="Severity">Effective severity — Error blocks, Warning is acknowledge-to-continue.</param>
/// <param name="AckToken">Stable identity of this exact warning; echoed back to acknowledge it.</param>
public sealed record StepFailure(
    string StepName,
    string ErrorCode,
    string Message,
    StepSeverity Severity = StepSeverity.Error,
    string? AckToken = null);

/// <summary>High-level outcome of a pipeline run.</summary>
public enum PipelineOutcome
{
    Success,
    ValidationsFailed,
    WarningsPending,
    ActionFailed
}

/// <summary>
/// Result returned by IActivityProcessPipeline after executing all configured steps.
/// </summary>
public sealed record PipelineResult(
    PipelineOutcome Outcome,
    IReadOnlyList<StepFailure> ValidationFailures,
    IReadOnlyList<StepFailure> Warnings,
    StepFailure? ActionFailure)
{
    /// <summary>True only when the pipeline fully passed and Actions committed.</summary>
    public bool IsSuccess => Outcome == PipelineOutcome.Success;

    /// <summary>True when only Warning-severity validations failed and need acknowledging; no mutation occurred.</summary>
    public bool RequiresAcknowledgement => Outcome == PipelineOutcome.WarningsPending;

    public static PipelineResult Success() =>
        new(PipelineOutcome.Success, [], [], null);

    public static PipelineResult ValidationsFailed(IReadOnlyList<StepFailure> failures) =>
        new(PipelineOutcome.ValidationsFailed, failures, [], null);

    public static PipelineResult WarningsPending(IReadOnlyList<StepFailure> warnings) =>
        new(PipelineOutcome.WarningsPending, [], warnings, null);

    public static PipelineResult ActionFailed(StepFailure failure) =>
        new(PipelineOutcome.ActionFailed, [], [], failure);

    /// <summary>Flattened error messages for backward-compatible response shaping.</summary>
    public IReadOnlyList<string> AllErrors()
    {
        var list = new List<string>();
        foreach (var f in ValidationFailures)
            list.Add(f.Message);
        if (ActionFailure is not null)
            list.Add(ActionFailure.Message);
        return list;
    }
}
