using Collateral.CollateralMasters.Models;
using Collateral.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Shared.Configuration;
using Shared.Data;
using Shared.Time;

namespace Collateral.CollateralMasters.Services;

/// <summary>
/// Hangfire daily job. Scans <c>collateral.ProjectDetails</c> for block-project records
/// whose last appraisal date is older than the configured interval and materializes the
/// result into <c>collateral.BlockReappraisalDue</c> for Phase C (the due-list screen).
///
/// Algorithm:
///   1. Read <c>BlockReappraisalIntervalYears</c> from system config (default 5).
///   2. Cross-schema Dapper query: join ProjectDetails + CollateralMasters, exclude
///      excluded/deleted masters, require LastAppraisedDate to be non-null and past due,
///      and require no active in-flight reappraisal (checked via appraisal.Appraisals).
///   3. Upsert via EF (all-status dict keyed by CollateralMasterId):
///      - Existing row (any status) → UpdateSnapshot; if Status=Consumed also Reactivate().
///      - No existing row → Create + Add.
///   4. Prune stale Pending rows whose CollateralMasterId is NOT in the current due set.
///      Consumed rows are never deleted (historical record).
///   5. Single SaveChangesAsync.
///
/// Idempotency: unique index on CollateralMasterId prevents duplicate rows.
/// Multi-server safety: Hangfire single-execution of a recurring job.
/// </summary>
public class BlockReappraisalJob(
    CollateralDbContext dbContext,
    ISqlConnectionFactory connectionFactory,
    ISystemConfigurationReader configReader,
    IDateTimeProvider dateTimeProvider,
    ILogger<BlockReappraisalJob> logger)
{
    private const string JobTag = "[REAPPRAISAL-BLOCK]";

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        logger.LogInformation("{Tag} Starting daily scan", JobTag);

        try
        {
            await RunScanAsync(ct);
        }
        catch (Exception ex)
        {
            // Re-throw so Hangfire records the run as failed (enabling retry/alerting) instead of
            // silently marking a failed scan as succeeded.
            logger.LogError(ex, "{Tag} Scan failed", JobTag);
            throw;
        }
    }

    private async Task RunScanAsync(CancellationToken ct)
    {
        // 1. Read interval from system config.
        var years = await configReader.GetIntAsync("BlockReappraisalIntervalYears", 5, ct);

        // Guard against an invalid admin-entered value (0 or negative) which would make the
        // due-date calculation meaningless and could flood the due list.
        if (years <= 0)
        {
            logger.LogWarning(
                "{Tag} Configured interval {Years} is invalid; falling back to default 5 years", JobTag, years);
            years = 5;
        }

        logger.LogInformation("{Tag} Using interval {Years} years", JobTag, years);

        // 2. Cross-schema Dapper query for due candidates.
        var candidates = await FetchDueCandidatesAsync(years);
        logger.LogInformation("{Tag} Found {Count} due candidate(s)", JobTag, candidates.Count);

        // 3. Load ALL existing rows (all statuses) keyed by CollateralMasterId.
        //    Must include Consumed rows so we can reactivate them instead of inserting a
        //    duplicate that would violate the unique index on CollateralMasterId.
        var existing = await dbContext.BlockReappraisalDue
            .ToDictionaryAsync(r => r.CollateralMasterId, ct);

        var dueIds = new HashSet<Guid>(candidates.Count);

        foreach (var c in candidates)
        {
            dueIds.Add(c.CollateralMasterId);

            if (existing.TryGetValue(c.CollateralMasterId, out var row))
            {
                // Refresh snapshot fields regardless of current Status.
                row.UpdateSnapshot(
                    c.ProjectName,
                    c.ProjectType,
                    c.OldAppraisalNumber,
                    c.ProjectSellingPrice,
                    c.TotalUnits,
                    c.RemainingUnits,
                    c.LastAppraisedDate,
                    c.DueDate);

                // A previously Consumed row that is due again must be reopened so Phase C
                // surfaces it. Without this, the unique index would reject a new Create().
                if (row.Status == "Consumed")
                    row.Reactivate();
            }
            else
            {
                var newRow = BlockReappraisalDue.Create(
                    c.CollateralMasterId,
                    c.ProjectName,
                    c.ProjectType,
                    c.OldAppraisalNumber,
                    c.ProjectSellingPrice,
                    c.TotalUnits,
                    c.RemainingUnits,
                    c.LastAppraisedDate,
                    c.DueDate);
                dbContext.BlockReappraisalDue.Add(newRow);
            }
        }

        // 4. Remove stale rows whose Status is still 'Pending' and that are no longer due
        //    (project became excluded, in-flight, or not yet past the interval again).
        //    Consumed rows are intentionally left untouched — they are historical records.
        var stale = existing.Values
            .Where(r => r.Status == "Pending" && !dueIds.Contains(r.CollateralMasterId))
            .ToList();

        if (stale.Count > 0)
        {
            dbContext.BlockReappraisalDue.RemoveRange(stale);
            logger.LogInformation("{Tag} Removing {Count} stale Pending row(s)", JobTag, stale.Count);
        }

        // 5. Single save.
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "{Tag} Scan complete. Upserted={Upserted} Stale removed={Stale}",
            JobTag, candidates.Count, stale.Count);
    }

    /// <summary>
    /// Cross-schema Dapper read: projects due for reappraisal.
    ///
    /// Due = all of:
    ///   - CollateralType = 'PRJ', IsMaster = 1, IsDeleted = 0, ExcludedFromReappraisal = 0
    ///   - LastAppraisedDate IS NOT NULL
    ///   - DATEADD(YEAR, @Years, LastAppraisedDate) &lt;= today (application-local date)
    ///   - No active in-flight reappraisal (Status NOT IN ('Completed','Cancelled'))
    /// </summary>
    private async Task<IReadOnlyList<DueCandidate>> FetchDueCandidatesAsync(int years)
    {
        const string sql = """
            SELECT
                cm.Id                                              AS CollateralMasterId,
                pd.ProjectName,
                pd.ProjectType,
                le.AppraisalNumber                                 AS OldAppraisalNumber,
                pd.ProjectSellingPrice,
                pd.TotalUnits,
                pd.RemainingUnits,
                le.AppraisalDate                                   AS LastAppraisedDate,
                DATEADD(YEAR, @Years, le.AppraisalDate)            AS DueDate
            FROM collateral.ProjectDetails pd
            JOIN collateral.CollateralMasters cm ON cm.Id = pd.CollateralMasterId
            -- The project's last appraisal, taken from its engagements rather than from
            -- ProjectDetails.LastAppraisal*. Those columns are a latest-WRITE-wins cache: the backfill
            -- job walks appraisals oldest-first, so an older one completing late overwrote them with
            -- stale values and pushed the due date backwards with no error. Engagements are immutable
            -- per appraisal, so ordering by AppraisalDate gives the genuinely latest round.
            OUTER APPLY (
                SELECT TOP 1 e.AppraisalId, e.AppraisalNumber, e.AppraisalDate
                FROM   collateral.CollateralEngagements e
                WHERE  e.CollateralMasterId = cm.Id
                ORDER BY e.AppraisalDate DESC, e.CreatedAt DESC, e.Id DESC
            ) le
            WHERE cm.CollateralType = 'PRJ'
              AND cm.IsMaster        = 1
              AND cm.IsDeleted       = 0
              AND cm.ExcludedFromReappraisal = 0
              AND pd.IsDeleted       = 0
              AND le.AppraisalDate IS NOT NULL
              AND DATEADD(YEAR, @Years, le.AppraisalDate) <= @Today
              AND NOT EXISTS (
                  SELECT 1
                  FROM appraisal.Appraisals a
                  WHERE a.PrevAppraisalId = le.AppraisalId
                    AND a.IsDeleted = 0
                    AND a.Status NOT IN ('Completed', 'Cancelled')
              )
            """;

        var parameters = new DynamicParameters();
        parameters.Add("Years", years);
        // Application-local "today" (Bangkok), so the due-date boundary matches the bank's calendar
        // date regardless of the SQL/host server clock. Sourced from appsettings via IDateTimeProvider.
        parameters.Add("Today", dateTimeProvider.ApplicationNow.Date);

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<DueCandidate>(sql, parameters);
        return rows.AsList();
    }

    private sealed record DueCandidate(
        Guid CollateralMasterId,
        string? ProjectName,
        string ProjectType,
        string? OldAppraisalNumber,
        decimal? ProjectSellingPrice,
        int TotalUnits,
        int RemainingUnits,
        DateTime? LastAppraisedDate,
        DateTime DueDate);
}
