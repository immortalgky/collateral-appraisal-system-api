using Workflow.Data.Entities;

namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Persists execution trace rows for a completed pipeline run.
/// Uses its own DB connection so trace rows survive an outer transaction rollback.
/// </summary>
public interface IActivityProcessExecutionSink
{
    /// <summary>
    /// Writes all provided trace rows in a single independent transaction.
    /// Any failure is swallowed and logged — trace rows must never block the completion path.
    /// </summary>
    Task PersistAsync(
        IReadOnlyList<ActivityProcessExecution> rows,
        CancellationToken ct);
}
