using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Workflow.Data.Entities;

namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Writes ActivityProcessExecution trace rows on a dedicated connection + transaction,
/// independent of the ambient completion transaction.
/// This ensures trace rows survive an outer rollback (e.g., on Action failure).
/// </summary>
public sealed class ActivityProcessExecutionSink(
    IConfiguration configuration,
    ILogger<ActivityProcessExecutionSink> logger) : IActivityProcessExecutionSink
{
    public async Task PersistAsync(
        IReadOnlyList<ActivityProcessExecution> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0) return;

        try
        {
            var connectionString = configuration.GetConnectionString("Database")
                ?? throw new InvalidOperationException("Connection string 'Database' is not configured.");

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);

            await using var tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

            const string sql = """
                INSERT INTO workflow.ActivityProcessExecutions
                    (Id, WorkflowInstanceId, WorkflowActivityExecutionId, ConfigurationId,
                     ConfigurationVersion, StepName, Kind, SortOrder,
                     RunIfExpressionSnapshot, ParametersJsonSnapshot,
                     Outcome, SkipReason, DurationMs, ErrorMessage, CreatedOn)
                VALUES
                    (@Id, @WorkflowInstanceId, @WorkflowActivityExecutionId, @ConfigurationId,
                     @ConfigurationVersion, @StepName, @Kind, @SortOrder,
                     @RunIfExpressionSnapshot, @ParametersJsonSnapshot,
                     @Outcome, @SkipReason, @DurationMs, @ErrorMessage, @CreatedOn)
                """;

            foreach (var row in rows)
            {
                var p = new DynamicParameters();
                p.Add("@Id", row.Id);
                p.Add("@WorkflowInstanceId", row.WorkflowInstanceId);
                p.Add("@WorkflowActivityExecutionId", row.WorkflowActivityExecutionId);
                p.Add("@ConfigurationId", row.ConfigurationId);
                p.Add("@ConfigurationVersion", row.ConfigurationVersion);
                p.Add("@StepName", row.StepName);
                p.Add("@Kind", (byte)row.Kind);
                p.Add("@SortOrder", row.SortOrder);
                p.Add("@RunIfExpressionSnapshot", row.RunIfExpressionSnapshot);
                p.Add("@ParametersJsonSnapshot", row.ParametersJsonSnapshot);
                p.Add("@Outcome", (byte)row.Outcome);
                p.Add("@SkipReason", row.SkipReason.HasValue ? (byte?)row.SkipReason.Value : null);
                p.Add("@DurationMs", row.DurationMs);
                p.Add("@ErrorMessage", row.ErrorMessage);
                p.Add("@CreatedOn", row.CreatedOn);

                await connection.ExecuteAsync(sql, p, transaction: (IDbTransaction)tx);
            }

            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            // Trace failures must never block the completion path
            logger.LogError(ex,
                "Failed to persist {Count} ActivityProcessExecution trace rows", rows.Count);
        }
    }
}
