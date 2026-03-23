namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Result of a single process step execution.
/// </summary>
public record ProcessStepResult
{
    public bool Success { get; init; }
    public List<string> Errors { get; init; } = [];

    public static ProcessStepResult Ok() => new() { Success = true };

    public static ProcessStepResult Fail(params string[] errors) =>
        new() { Success = false, Errors = [..errors] };
}
