using NJsonSchema;
using NJsonSchema.Generation;
using System.Text.Json;
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
    string? Description = null,
    /// <summary>
    /// Optional realistic example ParametersJson payload for this step.
    /// Shown in the admin UI as a starting template and must be valid against
    /// <see cref="ParametersSchema"/>.
    /// </summary>
    string? ExampleParametersJson = null)
{
    /// <summary>
    /// NJsonSchema generator settings that produce camelCase property names, matching the
    /// System.Text.Json PropertyNameCaseInsensitive convention used by GetParameters&lt;T&gt;()
    /// and by all ParametersJson stored in DB config rows.
    /// additionalProperties:false is preserved (NJsonSchema default).
    /// </summary>
    private static readonly SystemTextJsonSchemaGeneratorSettings _camelCaseSettings = new()
    {
        SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }
    };

    /// <summary>
    /// Convenience factory that generates a camelCase JSON Schema from a typed parameters record.
    /// Property names in the schema match the camelCase keys used in ParametersJson config rows
    /// and in <see cref="ProcessStepContext.GetParameters{TParams}()"/>.
    /// </summary>
    public static StepDescriptor For<TParams>(
        string name,
        string displayName,
        StepKind kind,
        string? description = null,
        string? exampleParametersJson = null)
    {
        var schema = JsonSchema.FromType<TParams>(_camelCaseSettings);
        return new StepDescriptor(name, displayName, kind, schema, description, exampleParametersJson);
    }
}
