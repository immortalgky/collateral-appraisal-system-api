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
    int AppraisalsWithNoEngagement,
    int ProjectUnitsUpdated,
    int ProjectUnitsUnmatched,
    string? Error);

/// <summary>
/// One-shot background job that copies the AS400 <c>HostCollateralId</c> already stamped on the
/// appraisal source rows — data carried over by the legacy-system migration — onto that appraisal's
/// <c>collateral.CollateralEngagements</c> row.
///
/// <b>Why this still exists.</b> The nightly HOST_COLLATERAL_LINK feed is the authoritative source
/// going forward, but it is a delta feed and will never re-announce a drawdown AS400 already sent.
/// For collateral carried over from the legacy system the only copy of the id lives on the
/// <c>appraisal.*</c> rows, so this job is how that history reaches the collateral side.
///
/// <b>Target changed.</b> It used to write <c>CollateralMasters.HostCollateralId</c>, which no longer
/// exists. The id now lives on the engagement, which is 1:1 with an appraisal and therefore matches
/// the grain AS400 sends — see <c>CollateralEngagement.HostCollateralId</c>.
///
/// Standalone by design — it does NOT go through <see cref="ICollateralMasterUpsertService"/>. It runs
/// one idempotent cross-schema SQL batch (all modules share one database), in two parts because the
/// AS400 id has two different grains:
///
/// <b>Part 1 — ordinary collateral, one id per appraisal → the appraisal's collateral master.</b>
///   - Gathered from the 5 in-scope source tables (Land titles, Building, Condo, Machinery, Lease) and
///     de-duplicated per AppraisalId. Vehicle/Vessel excluded — they never enter the Collateral module.
///   - The engagement (UNIQUE on AppraisalId) is only the route from an appraisal to its master; the
///     write lands on <c>collateral.CollateralMasters</c>, which is where AS400 state now lives.
///   - Only masters whose HostCollateralId IS NULL are written, so re-runs are no-ops and a value
///     already supplied by the live feed is never overwritten.
///   - Appraisals whose source rows carry more than one distinct HostCollateralId are skipped and
///     reported — the id is one-per-appraisal here, so a conflict means bad legacy data.
///   - IsRedeemed is left 0: AS400 only mints an id at drawdown, so having one means the collateral
///     was pledged. These rows cannot tell us about a later redemption; the nightly feed does that.
///
/// <b>Part 2 — block projects, one id per financed unit → the unit.</b>
///   - AS400 mints an id for each unit that has been sold and financed by the bank, so a project
///     appraisal owns many ids and its single engagement cannot hold them. They go to
///     <c>collateral.ProjectUnits.HostCollateralId</c> instead.
///   - Source is <c>appraisal.ProjectUnits</c> of the appraisal that last upserted the master, matched
///     unit-to-unit on sequence number plus room/plot identity.
///   - The PRJ engagement is deliberately left NULL, which keeps block projects out of
///     <c>vw_RegulatoryExport</c> until the per-unit export exists — better absent than reported with
///     one arbitrary unit's id against the whole project's value.
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
            jobId, dateTimeProvider.ApplicationNow, null, BackfillJobState.Started, 0, 0, 0, 0, 0, null);

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
                AppraisalsWithNoEngagement = counts.AppraisalsWithNoEngagement,
                ProjectUnitsUpdated = counts.ProjectUnitsUpdated,
                ProjectUnitsUnmatched = counts.ProjectUnitsUnmatched
            };

            logger.LogInformation(
                "HostCollateralIdBackfillJob {JobId}: finished. Updated={Updated} SkippedConflicts={SkippedConflicts} "
                + "NoEngagement={NoEngagement} ProjectUnitsUpdated={ProjectUnitsUpdated} ProjectUnitsUnmatched={ProjectUnitsUnmatched}",
                jobId, counts.Updated, counts.SkippedConflicts, counts.AppraisalsWithNoEngagement,
                counts.ProjectUnitsUpdated, counts.ProjectUnitsUnmatched);

            if (counts.SkippedConflicts > 0)
                logger.LogWarning(
                    "HostCollateralIdBackfillJob {JobId}: {Count} appraisal(s) skipped because their source rows carry more than one distinct HostCollateralId.",
                    jobId, counts.SkippedConflicts);

            if (counts.AppraisalsWithNoEngagement > 0)
                logger.LogWarning(
                    "HostCollateralIdBackfillJob {JobId}: {Count} appraisal(s) carry a host id but have no CollateralEngagement, so nothing could be written for them.",
                    jobId, counts.AppraisalsWithNoEngagement);

            if (counts.ProjectUnitsUnmatched > 0)
                logger.LogWarning(
                    "HostCollateralIdBackfillJob {JobId}: {Count} project unit(s) carry a host id that could not be "
                    + "matched to a collateral unit — the unit's project is not the master's last appraisal, or its "
                    + "sequence/room/plot disagree.",
                    jobId, counts.ProjectUnitsUnmatched);
        }
        catch (Exception ex)
        {
            // State = Failed, not Completed. The previous version reported Completed with Error set,
            // so a run that never touched a row still looked successful to anyone reading the status.
            _jobs[jobId] = _jobs[jobId] with
            {
                State = BackfillJobState.Failed,
                CompletedAt = dateTimeProvider.ApplicationNow,
                Error = ex.Message
            };
            logger.LogError(ex, "HostCollateralIdBackfillJob {JobId}: failed", jobId);
        }
    }

    private sealed record HostIdBackfillCounts(
        int Updated,
        int SkippedConflicts,
        int AppraisalsWithNoEngagement,
        int ProjectUnitsUpdated,
        int ProjectUnitsUnmatched);

    // One idempotent cross-schema batch, in two parts — see the class remarks for why the grain differs.
    //
    // The appraisal.* HostCollateralId columns are created by
    // Database/Migration/Scripts/20260808120100_Schema_AppraisalHostCollateralIdColumns.sql, which
    // brings a fresh database in line with production, where the legacy-system migration filled them.
    //
    // That script creates the column on 8 tables; this batch reads 6 of them. Vehicle and Vessel are
    // the exceptions: they never enter the Collateral module (the in-scope types are L, LB, U, MAC,
    // LSL, LSB, LS, PRJ), so they have neither an engagement nor a unit row to write to.
    private const string BackfillSql = """
        -- =====================================================================
        -- Part 1 — ordinary collateral: one id per appraisal, stamped on the engagement
        -- =====================================================================

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

        -- Writes the master, reached through the appraisal's engagement. Only fills NULLs, so a value
        -- already supplied by the nightly HOST_COLLATERAL_LINK feed (the authoritative source) is
        -- never overwritten.
        --
        -- Two appraisals of the same collateral both carrying a legacy id would resolve to one
        -- master; the NULL guard means the first one wins and the second is a no-op. That is
        -- acceptable here because this is a one-off recovery of legacy data and the ids agree in
        -- practice — the live feed, which does know the event dates, settles any real disagreement.
        UPDATE cm
        SET cm.HostCollateralId = t.HostCollateralId
        FROM collateral.CollateralMasters cm
        JOIN collateral.CollateralEngagements ce ON ce.CollateralMasterId = cm.Id
        JOIN #HostIdPerAppraisal t ON t.AppraisalId = ce.AppraisalId
        WHERE cm.HostCollateralId IS NULL
          AND t.DistinctCount = 1;

        SET @Updated = @@ROWCOUNT;

        -- =====================================================================
        -- Part 2 — block projects: one id per financed unit, stamped on the unit
        --
        -- Source is the appraisal that last upserted the master (ProjectDetails.LastAppraisalId),
        -- because ProjectDetail.ReplaceUnits rebuilds the whole unit set from that appraisal — its
        -- sequence numbers are the ones the collateral units carry. Room/plot must also agree, so a
        -- shifted sequence cannot silently attach a unit's id to its neighbour.
        -- =====================================================================

        DECLARE @ProjectUnitsUpdated int;

        SELECT apu.Id AS AppraisalUnitId,
               cpu.Id AS CollateralUnitId,
               apu.HostCollateralId
        INTO #ProjectUnitHostIds
        FROM appraisal.ProjectUnits apu
        JOIN appraisal.Projects ap        ON ap.Id = apu.ProjectId
        -- Master resolved through the engagement (UNIQUE on AppraisalId) rather than
        -- ProjectDetails.LastAppraisalId, which has been removed.
        JOIN collateral.CollateralEngagements ce ON ce.AppraisalId = ap.AppraisalId
        JOIN collateral.ProjectDetails pd ON pd.CollateralMasterId = ce.CollateralMasterId
                                         AND pd.IsDeleted = 0
        JOIN collateral.ProjectUnits cpu  ON cpu.CollateralMasterId = pd.CollateralMasterId
                                         AND cpu.SequenceNumber     = apu.SequenceNumber
                                         AND ISNULL(cpu.RoomNumber, N'') = ISNULL(apu.RoomNumber, N'')
                                         AND ISNULL(cpu.PlotNumber, N'') = ISNULL(apu.PlotNumber, N'')
        WHERE apu.HostCollateralId IS NOT NULL;

        -- Only fills NULLs, so re-runs are no-ops and a value carried over by
        -- CollateralMasterUpsertService.CarryOverHostCollateralIds is never overwritten.
        UPDATE cpu
        SET cpu.HostCollateralId = t.HostCollateralId
        FROM collateral.ProjectUnits cpu
        JOIN #ProjectUnitHostIds t ON t.CollateralUnitId = cpu.Id
        WHERE cpu.HostCollateralId IS NULL;

        SET @ProjectUnitsUpdated = @@ROWCOUNT;

        SELECT
            @Updated AS Updated,
            (SELECT COUNT(*) FROM #HostIdPerAppraisal WHERE DistinctCount > 1) AS SkippedConflicts,
            (SELECT COUNT(*) FROM #HostIdPerAppraisal t
                WHERE t.DistinctCount = 1
                  AND NOT EXISTS (SELECT 1 FROM collateral.CollateralEngagements ce WHERE ce.AppraisalId = t.AppraisalId)
            ) AS AppraisalsWithNoEngagement,
            @ProjectUnitsUpdated AS ProjectUnitsUpdated,
            -- Every legacy unit id that found no collateral unit at all, whatever the reason.
            (SELECT COUNT(*) FROM appraisal.ProjectUnits WHERE HostCollateralId IS NOT NULL)
              - (SELECT COUNT(DISTINCT AppraisalUnitId) FROM #ProjectUnitHostIds) AS ProjectUnitsUnmatched;

        DROP TABLE #HostIdPerAppraisal;
        DROP TABLE #ProjectUnitHostIds;
        """;
}
