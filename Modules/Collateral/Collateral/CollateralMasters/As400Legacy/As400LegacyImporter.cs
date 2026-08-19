using System.Security.Cryptography;
using System.Text;
using Collateral.CollateralMasters.Models;
using Collateral.Contracts;
using Collateral.Contracts.As400Legacy;
using Collateral.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Time;

namespace Collateral.CollateralMasters.As400Legacy;

/// <summary>
/// Brings the AS400 legacy collateral listing into the collateral store.
///
/// <b>What the source gives us, and what it does not.</b> Each row of
/// <c>appraisal.AS400ReportListing</c> carries an application number (<c>99A…</c>), an AS400
/// collateral id, a valuation date and a value. It carries NO title number and NO location — so
/// there is nothing to dedup on, and the collateral id is the only handle we have.
///
/// That handle decides each row's fate, and the order of the tests matters:
///
///   1. the id already reaches a CollateralMaster → attach the valuation there. The collateral is
///      one we already know under a newer CAS appraisal, and this is its older history.
///   2. otherwise, and AS400 still reports the application number → mint an unidentified master.
///      Still being reported under its 99A number means it was never re-appraised in CAS.
///   3. otherwise → skip. Absent from the feed means the bank has released it; creating a master
///      would be inventing an asset.
///
/// <b>Rule 1 before rule 2, always.</b> A handful of rows satisfy both — AS400 keeps reporting them
/// under the 99A number even though a CAS appraisal exists — and minting for those would leave two
/// masters for one physical collateral, with no tool to merge them.
///
/// <b>Idempotent by construction.</b> The synthetic AppraisalId is derived from the application
/// number and <c>UX_CollateralEngagements_Appraisal</c> is unique on it, so a re-run finds its own
/// work already present. No <c>appraisal.Appraisals</c> row is created: those columns carry no
/// foreign key, and inventing appraisals would surface them on every screen that lists appraisals.
/// </summary>
public class As400LegacyImporter(
    CollateralDbContext dbContext,
    ISqlConnectionFactory connectionFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<As400LegacyImporter> logger) : IAs400LegacyImporter
{
    /// <summary>Stamped on every engagement this importer creates; downstream filters key off it.</summary>
    public const string LegacyAppraisalType = "AS400Legacy";

    private const int SaveEvery = 500;

    public async Task<As400LegacyImportResult> ImportAsync(
        IReadOnlySet<string> stillReportedApplicationNumbers,
        CancellationToken cancellationToken = default)
    {
        // appraisal.AS400ReportListing is supplied by the bank, not created by our migrations, so on
        // a database where it has not been loaded yet the query below would fail with SQL error 208
        // ("Invalid object name") — accurate but unhelpful. Say what is actually missing instead.
        if (!await ListingTableExistsAsync())
            throw new InvalidOperationException(
                "appraisal.AS400ReportListing does not exist. The bank supplies this table as a "
                + "one-time load of the AS400 legacy collateral listing; the import cannot run until "
                + "it has been created and populated on this database.");

        var rows = await LoadListingAsync();
        logger.LogInformation(
            "[AS400-LEGACY-IMPORT] {Rows} listing row(s); {Reported} application number(s) still reported "
            + "by AS400", rows.Count, stillReportedApplicationNumbers.Count);

        var reachable = await LoadReachableMastersAsync(cancellationToken);

        var alreadyImported = await dbContext.CollateralEngagements
            .Where(e => e.AppraisalType == LegacyAppraisalType)
            .Select(e => e.AppraisalId)
            .ToHashSetAsync(cancellationToken);

        int attached = 0, created = 0, notHeld = 0, wouldBeLatest = 0, already = 0, pending = 0;

        foreach (var row in rows)
        {
            var appraisalId = DeterministicAppraisalId(row.ApplicationId);
            if (!alreadyImported.Add(appraisalId))
            {
                already++;
                continue;
            }

            if (reachable.TryGetValue(row.CollateralId, out var target))
            {
                // Guarded on the date. This is a data-sanity check, not the export safeguard:
                // CollateralResultQuery picks its representative from non-legacy engagements only,
                // so a legacy row can never speak for the master no matter how it is dated. What
                // this rejects is a legacy valuation claiming to be NEWER than our own most recent
                // appraisal — that contradiction deserves a human, not an automatic import.
                // Equal dates are fine and common: AS400 and CAS valuing on the same day.
                if (target.Latest is not null && row.ValuationDate > target.Latest)
                {
                    wouldBeLatest++;
                    logger.LogWarning(
                        "[AS400-LEGACY-IMPORT] {AppId} (valued {ValuationDate:yyyy-MM-dd}) is newer than "
                        + "every engagement on master {MasterId} (latest {Latest:yyyy-MM-dd}); skipped so it "
                        + "cannot become the latest and reach the outbound file. Needs a human decision.",
                        row.ApplicationId, row.ValuationDate, target.MasterId, target.Latest);
                    continue;
                }

                var master = await dbContext.CollateralMasters
                    .FirstAsync(m => m.Id == target.MasterId, cancellationToken);
                AppendLegacyEngagement(master, row, appraisalId);
                attached++;
            }
            else if (stillReportedApplicationNumbers.Contains(row.ApplicationId))
            {
                var master = CollateralMaster.CreateUnidentified(ownerName: null);
                dbContext.CollateralMasters.Add(master);
                AppendLegacyEngagement(master, row, appraisalId);
                created++;
            }
            else
            {
                notHeld++;
                continue;
            }

            if (++pending >= SaveEvery)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                pending = 0;
            }
        }

        if (pending > 0) await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "[AS400-LEGACY-IMPORT] considered={Considered} attached={Attached} created={Created} "
            + "skippedNotHeld={NotHeld} skippedWouldBeLatest={WouldBeLatest} alreadyPresent={Already}",
            rows.Count, attached, created, notHeld, wouldBeLatest, already);

        return new As400LegacyImportResult(rows.Count, attached, created, notHeld, wouldBeLatest, already);
    }

    private void AppendLegacyEngagement(CollateralMaster master, ListingRow row, Guid appraisalId)
        => master.AppendEngagement(
            appraisalId: appraisalId,
            appraisalNumber: row.ApplicationId,
            // No request ever existed: the valuation happened inside AS400, not through an
            // application in this system. The column is NOT NULL but carries no foreign key.
            requestId: Guid.Empty,
            requestNumber: string.Empty,
            appraisalType: LegacyAppraisalType,
            appraisalDate: row.ValuationDate,
            appraiserUserId: null,
            appraisalCompanyId: null,
            appraisalCompanyName: null,
            constructionInspectionFeeAmount: null,
            // The listing has no per-group breakdown to snapshot. An empty object keeps the column's
            // contract — readers parse it as JSON — without pretending there is structure to read.
            snapshot: "{}",
            createdAt: dateTimeProvider.ApplicationNow,
            appraisedCollateralType: CollateralTypes.Unidentified,
            appraisalValue: row.ValuationPrice,
            isUnderConstruction: row.UnderConstruction,
            constructionProgressPercent: row.ProcessOfConstruction);

    /// <summary>
    /// Every AS400 collateral id that already reaches a master, with that master's newest engagement
    /// date. Two independent routes are unioned so the importer works whether or not the link feed
    /// has run yet:
    ///   - the id sits on the master (written by the nightly feed), and
    ///   - the id sits on an appraisal property carried over by the legacy migration.
    /// Loaded once — the listing is small but the master table is not, and per-row lookups would
    /// otherwise cost tens of thousands of round-trips.
    /// </summary>
    private async Task<Dictionary<string, ReachableMaster>> LoadReachableMastersAsync(CancellationToken ct)
    {
        const string sql = """
            WITH Reach AS (
                SELECT m.HostCollateralId AS CollateralId, m.Id AS MasterId
                FROM collateral.CollateralMasters m
                WHERE m.HostCollateralId IS NOT NULL AND m.IsDeleted = 0

                UNION

                SELECT h.HostCollateralId, e.CollateralMasterId
                FROM (
                    SELECT lad.AppraisalPropertyId AS PropertyId, lt.HostCollateralId
                      FROM appraisal.LandTitles lt
                      JOIN appraisal.LandAppraisalDetails lad ON lad.Id = lt.LandAppraisalDetailId
                     WHERE lt.HostCollateralId IS NOT NULL
                    UNION ALL SELECT AppraisalPropertyId, HostCollateralId FROM appraisal.BuildingAppraisalDetails  WHERE HostCollateralId IS NOT NULL
                    UNION ALL SELECT AppraisalPropertyId, HostCollateralId FROM appraisal.CondoAppraisalDetails     WHERE HostCollateralId IS NOT NULL
                    UNION ALL SELECT AppraisalPropertyId, HostCollateralId FROM appraisal.MachineryAppraisalDetails WHERE HostCollateralId IS NOT NULL
                    UNION ALL SELECT AppraisalPropertyId, HostCollateralId FROM appraisal.LeaseAgreementDetails     WHERE HostCollateralId IS NOT NULL
                ) h
                JOIN appraisal.AppraisalProperties p ON p.Id = h.PropertyId
                JOIN collateral.CollateralEngagements e ON e.AppraisalId = p.AppraisalId
            )
            SELECT r.CollateralId,
                   r.MasterId,
                   (SELECT MAX(e2.AppraisalDate) FROM collateral.CollateralEngagements e2
                     WHERE e2.CollateralMasterId = r.MasterId) AS Latest
            FROM Reach r
            """;

        var rows = await connectionFactory.QueryAsync<ReachableMaster>(sql);

        // One id can reach two masters when the legacy data disagrees with the feed. Keep the first
        // deterministically rather than throwing: attaching history to one of them is recoverable,
        // failing the whole import is not.
        var result = new Dictionary<string, ReachableMaster>(StringComparer.Ordinal);
        foreach (var r in rows.OrderBy(x => x.MasterId))
        {
            if (result.TryAdd(r.CollateralId, r)) continue;
            logger.LogWarning(
                "[AS400-LEGACY-IMPORT] Collateral id {CollateralId} reaches more than one master; "
                + "using {Chosen} and ignoring {Ignored}", r.CollateralId, result[r.CollateralId].MasterId, r.MasterId);
        }

        return result;
    }

    private async Task<bool> ListingTableExistsAsync()
        => await connectionFactory.QueryFirstOrDefaultAsync<int?>(
               "SELECT 1 WHERE OBJECT_ID('appraisal.AS400ReportListing') IS NOT NULL") is not null;

    private async Task<List<ListingRow>> LoadListingAsync()
    {
        const string sql = """
            SELECT
                RTRIM(r.ApplicationId)                               AS ApplicationId,
                CAST(CAST(r.CollateralID AS bigint) AS nvarchar(25)) AS CollateralId,
                r.ValuationDate,
                CAST(r.ValuationPriceInBaht AS decimal(18,2))        AS ValuationPrice,
                CASE WHEN r.UnderConstruction = 'Y' THEN CAST(1 AS bit)
                     WHEN r.UnderConstruction = 'N' THEN CAST(0 AS bit)
                     ELSE NULL END                                   AS UnderConstruction,
                CAST(r.ProcessOfConstruction AS decimal(7,4))        AS ProcessOfConstruction
            FROM appraisal.AS400ReportListing r
            WHERE r.ValuationDate IS NOT NULL
              AND r.ValuationDate > '1901-01-01'   -- 1900-01-01 is the source's "no date" placeholder
            ORDER BY r.ValuationDate
            """;

        var rows = await connectionFactory.QueryAsync<ListingRow>(sql);
        return rows.ToList();
    }

    /// <summary>
    /// A stable GUID per application number, so a re-run recognises what it already wrote.
    /// The hash is a hash-to-GUID function here, not a security primitive: the input is a public
    /// application number and the only requirement is that it always maps to the same 16 bytes.
    /// SHA-256 truncated to those 16 bytes, rather than MD5, so no weak algorithm appears anywhere
    /// in the codebase — the same choice HostCollateralLinkFileParser already makes for its RowHash.
    ///
    /// <b>Changing this changes every id it derives.</b> A database that already holds engagements
    /// from an earlier run must have them cleared before re-running, or the second run mints a
    /// duplicate engagement for every listing row instead of recognising its own work.
    /// </summary>
    private static Guid DeterministicAppraisalId(string applicationNumber)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes($"AS400Legacy:{applicationNumber}")).AsSpan(0, 16));

    private sealed record ListingRow
    {
        public string ApplicationId { get; init; } = null!;
        public string CollateralId { get; init; } = null!;
        public DateTime ValuationDate { get; init; }
        public decimal? ValuationPrice { get; init; }
        public bool? UnderConstruction { get; init; }
        public decimal? ProcessOfConstruction { get; init; }
    }

    private sealed record ReachableMaster
    {
        public string CollateralId { get; init; } = null!;
        public Guid MasterId { get; init; }
        public DateTime? Latest { get; init; }
    }
}
