using System.Text.Json;

namespace Workflow.Workflow.Models;

/// <summary>
/// Typed accessors over the loose <see cref="WorkflowInstance.Variables"/> dictionary.
/// </summary>
public static class WorkflowVariables
{
    /// <summary>
    /// Resolve the "appraisalId" variable to a <see cref="Guid"/>, tolerating Guid / string / JsonElement
    /// representations produced by JSON round-trips through the persistence layer.
    /// </summary>
    public static Guid? TryGetAppraisalId(IReadOnlyDictionary<string, object> variables)
    {
        if (!variables.TryGetValue("appraisalId", out var raw) || raw is null)
            return null;

        return raw switch
        {
            Guid g => g,
            string s when Guid.TryParse(s, out var parsed) => parsed,
            JsonElement je when je.ValueKind == JsonValueKind.String
                && Guid.TryParse(je.GetString(), out var jp) => jp,
            _ => null
        };
    }
}
