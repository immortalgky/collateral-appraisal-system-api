using Workflow.Data.Entities;

namespace Workflow.Services.Configuration;

/// <summary>
/// Resolves the initial-routing decision from the admin-configured
/// <see cref="AutoAssignmentRule"/> table.
/// </summary>
public interface IAutoAssignmentRuleService
{
    /// <summary>
    /// Returns the first active rule (ascending Priority) whose conditions all match the supplied
    /// workflow variables, or null when the table is empty or nothing matches — in which case the
    /// caller must fall back to the workflow definition's own routing conditions.
    /// </summary>
    Task<AutoAssignmentRule?> FindMatchingRuleAsync(
        IReadOnlyDictionary<string, object> variables,
        CancellationToken ct = default);
}
