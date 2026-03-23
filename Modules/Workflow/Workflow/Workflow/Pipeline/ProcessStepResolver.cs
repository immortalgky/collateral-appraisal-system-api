namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Resolves IActivityProcessStep implementations by their Name property.
/// Collects all registered steps from DI and builds a name→step dictionary.
/// </summary>
public class ProcessStepResolver
{
    private readonly Dictionary<string, IActivityProcessStep> _steps;

    public ProcessStepResolver(IEnumerable<IActivityProcessStep> steps)
    {
        _steps = steps.ToDictionary(s => s.Name, s => s, StringComparer.OrdinalIgnoreCase);
    }

    public IActivityProcessStep? Resolve(string processorName)
    {
        _steps.TryGetValue(processorName, out var step);
        return step;
    }
}
