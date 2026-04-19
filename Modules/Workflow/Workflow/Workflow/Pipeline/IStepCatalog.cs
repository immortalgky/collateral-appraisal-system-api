using Workflow.Data.Entities;

namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Singleton catalog of all registered IActivityProcessStep descriptors.
/// Used by the admin API to validate configs and surface the step palette to the UI.
/// </summary>
public interface IStepCatalog
{
    /// <summary>Returns the descriptor for the given step name, or null if not registered.</summary>
    StepDescriptor? GetDescriptor(string name);

    /// <summary>Returns all registered step descriptors.</summary>
    IReadOnlyList<StepDescriptor> GetAll();

    /// <summary>Returns all descriptors for steps of the given kind.</summary>
    IReadOnlyList<StepDescriptor> GetByKind(StepKind kind);
}
