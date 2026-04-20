namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Discriminated result returned by every IActivityProcessStep.
/// </summary>
public abstract record ProcessStepResult
{
    private ProcessStepResult() { }

    /// <summary>Step ran successfully and the pipeline may continue.</summary>
    public sealed record Passed : ProcessStepResult;

    /// <summary>Step ran and detected a business rule violation.</summary>
    public sealed record Failed(string ErrorCode, string Message) : ProcessStepResult;

    /// <summary>Step threw an unhandled exception.</summary>
    public sealed record Errored(Exception Exception) : ProcessStepResult;

    // ── Convenience factories ──────────────────────────────────────────────

    public static ProcessStepResult Pass() => new Passed();

    public static ProcessStepResult Fail(string errorCode, string message) =>
        new Failed(errorCode, message);

    public static ProcessStepResult Error(Exception exception) =>
        new Errored(exception);

    public bool IsSuccess => this is Passed;
}
