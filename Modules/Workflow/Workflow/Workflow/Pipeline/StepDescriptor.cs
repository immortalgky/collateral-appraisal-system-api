using NJsonSchema;
using Workflow.Data.Entities;

namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Self-description emitted by every IActivityProcessStep.
/// Used by the admin UI to render forms and by the admin API to validate configurations.
/// </summary>
public sealed record StepDescriptor(
    /// <summary>Stable key used in config rows (e.g., "ValidateTaskOwnership").</summary>
    string Name,
    /// <summary>Human-readable label shown in the admin UI.</summary>
    string DisplayName,
    /// <summary>Whether this step is a Validation or an Action.</summary>
    StepKind Kind,
    /// <summary>NJsonSchema-generated schema for the step's Parameters record.</summary>
    JsonSchema ParametersSchema,
    /// <summary>Optional description of what this step does.</summary>
    string? Description = null)
{
    /// <summary>
    /// Convenience factory that generates the JSON Schema from a typed parameters record.
    /// </summary>
    public static StepDescriptor For<TParams>(
        string name,
        string displayName,
        StepKind kind,
        string? description = null)
    {
        var schema = JsonSchema.FromType<TParams>();
        return new StepDescriptor(name, displayName, kind, schema, description);
    }
}
