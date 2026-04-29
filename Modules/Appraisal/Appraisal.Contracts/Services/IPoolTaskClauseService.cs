namespace Appraisal.Contracts.Services;

/// <summary>
/// Cross-module contract: builds the SQL fragment + parameters that gate
/// pool-task visibility for a given caller.
///
/// Implemented in the Workflow module (delegates to PoolTaskAccess.BuildSqlClause)
/// so the Appraisal module never takes a direct dependency on Workflow internals.
/// </summary>
public interface IPoolTaskClauseService
{
    /// <summary>
    /// Builds the SQL WHERE clause and its parameters for pool-task ownership filtering.
    /// Includes the caller's username in the candidate set so direct-assignment tasks
    /// (AssignedType='1', e.g. rm-pick-winner or claimed tasks) are also visible.
    /// Returns null when the caller has no identity — the caller should return an empty result.
    /// </summary>
    Task<PoolTaskClause?> BuildClauseForCurrentUserAsync(CancellationToken cancellationToken = default);
}

/// <summary>SQL fragment + bound parameters for pool-task ownership.</summary>
public sealed record PoolTaskClause(string Sql, IReadOnlyDictionary<string, object?> Parameters);
