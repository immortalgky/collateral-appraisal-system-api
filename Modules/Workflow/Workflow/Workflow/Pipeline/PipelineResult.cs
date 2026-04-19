namespace Workflow.Workflow.Pipeline;

/// <summary>Describes a single step failure surfaced in the pipeline result.</summary>
public sealed record StepFailure(string StepName, string ErrorCode, string Message);

/// <summary>
/// Result returned by IActivityProcessPipeline after executing all configured steps.
/// </summary>
public sealed record PipelineResult(
    bool IsSuccess,
    IReadOnlyList<StepFailure> ValidationFailures,
    StepFailure? ActionFailure)
{
    public static PipelineResult Success() =>
        new(true, [], null);

    public static PipelineResult ValidationsFailed(IReadOnlyList<StepFailure> failures) =>
        new(false, failures, null);

    public static PipelineResult ActionFailed(StepFailure failure) =>
        new(false, [], failure);

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
