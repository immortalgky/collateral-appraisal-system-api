namespace Workflow.Workflow.Pipeline;

/// <summary>
/// A named process step that executes during activity completion.
/// Implementations are resolved from DI by their Name property.
/// </summary>
public interface IActivityProcessStep
{
    string Name { get; }
    Task<ProcessStepResult> ExecuteAsync(ProcessStepContext context, CancellationToken ct);
}
