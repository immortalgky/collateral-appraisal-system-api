using Workflow.Data.Entities;

namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Singleton implementation of IStepCatalog.
/// Built once from all DI-registered IActivityProcessStep instances at startup.
/// </summary>
public sealed class StepCatalog : IStepCatalog
{
    private readonly IReadOnlyDictionary<string, StepDescriptor> _byName;
    private readonly IReadOnlyList<StepDescriptor> _all;

    public StepCatalog(IEnumerable<IActivityProcessStep> steps)
    {
        var byName = new Dictionary<string, StepDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in steps)
        {
            byName[step.Descriptor.Name] = step.Descriptor;
        }
        _byName = byName;
        _all = byName.Values.OrderBy(d => d.Name).ToList();
    }

    public StepDescriptor? GetDescriptor(string name) =>
        _byName.TryGetValue(name, out var d) ? d : null;

    public IReadOnlyList<StepDescriptor> GetAll() => _all;

    public IReadOnlyList<StepDescriptor> GetByKind(StepKind kind) =>
        _all.Where(d => d.Kind == kind).ToList();
}
