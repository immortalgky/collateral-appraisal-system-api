namespace Workflow.Workflow.Pipeline;

/// <summary>
/// A named, self-describing pipeline step executed during activity completion.
/// Implementations are resolved from DI by their Descriptor.Name.
/// </summary>
public interface IActivityProcessStep
{
    /// <summary>
    /// Descriptor providing name, kind, parameter schema and display metadata.
    /// Must be stable across DI lifetimes — treat as a static manifest.
    /// </summary>
    StepDescriptor Descriptor { get; }

    Task<ProcessStepResult> ExecuteAsync(ProcessStepContext ctx, CancellationToken ct);
}
