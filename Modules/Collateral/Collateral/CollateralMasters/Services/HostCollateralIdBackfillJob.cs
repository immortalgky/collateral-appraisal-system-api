using System.Collections.Concurrent;
using Dapper;
using Microsoft.Extensions.Hosting;
using Shared.Data;
using Shared.Time;

namespace Collateral.CollateralMasters.Services;

// ---------------------------------------------------------------------------
// Host-collateral-id backfill — status tracked in-memory per run
// ---------------------------------------------------------------------------

public record HostCollateralIdBackfillStatus(
    Guid JobId,
    DateTime StartedAt,
    DateTime? CompletedAt,
    BackfillJobState State,
    int Updated,
    int SkippedConflicts,
    int AppraisalsWithNoMaster,
    string? Error);

/// <summary>
/// One-shot background job that copies the AS400 <c>HostCollateralId</c> already stamped on the
/// appraisal source detail rows onto the owning <c>collateral.CollateralMasters</c> row.
///
/// Standalone by design — it does NOT go through <see cref="ICollateralMasterUpsertService"/>. It runs a
/// single idempotent cross-schema SQL statement (all modules share one database):
///   - HostCollateralId is one-per-appraisal: gathered from the 5 in-scope source tables (Land titles,
///     Building, Condo, Machinery, Lease) and de-duplicated per AppraisalId. Vehicle/Vessel are excluded.
///   - The target master is the one that owns the appraisal's engagement
///     (collateral.CollateralEngagements has a UNIQUE AppraisalId → CollateralMasterId).
///   - Only rows where CollateralMasters.HostCollateralId IS NULL are written, so re-runs are no-ops.
///   - Appraisals whose source rows carry more than one distinct HostCollateralId are skipped (reported).
///
/// Fire-and-forget, exactly like <see cref="CollateralBackfillJob"/>: it must NOT capture the HTTP
/// request's CancellationToken (that token cancels when the response is sent). It runs under
/// <see cref="IHostApplicationLifetime.ApplicationStopping"/> and opens its own DI scope.
/// </summary>
public class HostCollateralIdBackfillJob(
    IServiceScopeFactory scopeFactory,
    ILogger<HostCollateralIdBackfillJob> logger,
    IDateTimeProvider dateTimeProvider,
    IHostApplicationLifetime lifetime)
{
    private readonly ConcurrentDictionary<Guid, HostCollateralIdBackfillStatus> _jobs = new();

    public HostCollateralIdBackfillStatus? GetJobStatus(Guid jobId)
        => _jobs.TryGetValue(jobId, out var status) ? status : null;

    /// <summary>
    /// Starts the backfill in the background and returns the JobId immediately.
    /// Any token passed here is intentionally ignored — see the class remarks.
    /// </summary>
    public Guid StartAsync(CancellationToken ct = default)
    {
        var jobId = Guid.CreateVersion7();
        _jobs[jobId] = new HostCollateralIdBackfillStatus(
            jobId, dateTimeProvider.ApplicationNow, null, BackfillJobState.Started, 0, 0, 0, null);

        var jobToken = lifetime.ApplicationStopping;
        _ = Task.Run(() => RunAsync(jobId, jobToken), jobToken);

        return jobId;
    }

    private async Task RunAsync(Guid jobId, CancellationToken ct)
    {
        _jobs[jobId] = _jobs[jobId] with { State = BackfillJobState.InProgress };
        logger.LogInformation("HostCollateralIdBackfillJob {JobId}: started", jobId);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
            var connection = connectionFactory.GetOpenConnection();

            var counts = await connection.QuerySingleAsync<HostIdBackfillCounts>(
                new CommandDefinition(BackfillSql, cancellationToken: ct));

            _jobs[jobId] = _jobs[jobId] with
            {
                State = BackfillJobState.Completed,
                CompletedAt = dateTimeProvider.ApplicationNow,
                Updated = counts.Updated,
                SkippedConflicts = counts.SkippedConflicts,
                AppraisalsWithNoMaster = counts.AppraisalsWithNoMaster
            };

            logger.LogInformation(
                "HostCollateralIdBackfillJob {JobId}: finished. Updated={Updated} SkippedConflicts={SkippedConflicts} AppraisalsWithNoMaster={NoMaster}",
                jobId, counts.Updated, counts.SkippedConflicts, counts.AppraisalsWithNoMaster);

            if (counts.SkippedConflicts > 0)
                logger.LogWarning(
                    "HostCollateralIdBackfillJob {JobId}: {Count} appraisal(s) skipped because their source rows carry more than one distinct HostCollateralId.",
                    jobId, counts.SkippedConflicts);
        }
        catch (Exception ex)
        {
            _jobs[jobId] = _jobs[jobId] with
            {
                State = BackfillJobState.Completed,
                CompletedAt = dateTimeProvider.ApplicationNow,
                Error = ex.Message
            };
            logger.LogError(ex, "HostCollateralIdBackfillJob {JobId}: failed", jobId);
        }
    }

    private sealed record HostIdBackfillCounts(int Updated, int SkippedConflicts, int AppraisalsWithNoMaster);

    // One idempotent cross-schema batch. HostCollateralId is one-per-appraisal, resolved from the 5
    // in-scope source tables and stamped onto the master that owns the appraisal's (unique) engagement.
    private const string BackfillSql = """
        SELECT p.AppraisalId,
               MAX(h.HostCollateralId)            AS HostCollateralId,
               COUNT(DISTINCT h.HostCollateralId) AS DistinctCount
        INTO #HostIdPerAppraisal
        FROM (
            SELECT lad.AppraisalPropertyId AS AppraisalPropertyId, lt.HostCollateralId
            FROM appraisal.LandTitles lt
            JOIN appraisal.LandAppraisalDetails lad ON lad.Id = lt.LandAppraisalDetailId
            WHERE lt.HostCollateralId IS NOT NULL
            UNION ALL SELECT AppraisalPropertyId, HostCollateralId FROM appraisal.BuildingAppraisalDetails  WHERE HostCollateralId IS NOT NULL
            UNION ALL SELECT AppraisalPropertyId, HostCollateralId FROM appraisal.CondoAppraisalDetails     WHERE HostCollateralId IS NOT NULL
            UNION ALL SELECT AppraisalPropertyId, HostCollateralId FROM appraisal.MachineryAppraisalDetails WHERE HostCollateralId IS NOT NULL
            UNION ALL SELECT AppraisalPropertyId, HostCollateralId FROM appraisal.LeaseAgreementDetails     WHERE HostCollateralId IS NOT NULL
        ) h
        JOIN appraisal.AppraisalProperties p ON p.Id = h.AppraisalPropertyId
        GROUP BY p.AppraisalId;

        DECLARE @Updated int;

        UPDATE cm SET cm.HostCollateralId = t.HostCollateralId
        FROM collateral.CollateralMasters cm
        JOIN collateral.CollateralEngagements ce ON ce.CollateralMasterId = cm.Id
        JOIN #HostIdPerAppraisal t ON t.AppraisalId = ce.AppraisalId
        WHERE cm.HostCollateralId IS NULL
          AND t.DistinctCount = 1;

        SET @Updated = @@ROWCOUNT;

        SELECT
            @Updated AS Updated,
            (SELECT COUNT(*) FROM #HostIdPerAppraisal WHERE DistinctCount > 1) AS SkippedConflicts,
            (SELECT COUNT(*) FROM #HostIdPerAppraisal t
                WHERE t.DistinctCount = 1
                  AND NOT EXISTS (SELECT 1 FROM collateral.CollateralEngagements ce WHERE ce.AppraisalId = t.AppraisalId)
            ) AS AppraisalsWithNoMaster;

        DROP TABLE #HostIdPerAppraisal;
        """;
}
