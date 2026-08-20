using Collateral.CollateralMasters.Models;
using Collateral.Contracts;
using Collateral.Contracts.HostLink;
using Collateral.Data;
using Collateral.Data.Repository;
using Integration.Contracts.HostLink;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Collateral.CollateralMasters.HostLink;

/// <summary>
/// Writes the AS400 collateral id and redemption state from a COLLATLINK file onto the
/// <c>CollateralMaster</c>.
///
/// <b>Why the master</b> — AS400 keys collateral, not appraisals: one id is minted per collateral at
/// drawdown and redemption is reported against that same id. The state is therefore the current
/// state of a physical thing, and the outbound COLLATERAL_RESULT sends one row per master carrying
/// the latest engagement's figures. Storing it per appraisal forced every reader to re-derive "which
/// appraisal speaks for this collateral right now", and each reader did it slightly differently.
///
/// <b>Why the engagement is still loaded</b> — the file addresses rows by appraisal number, which is
/// what our engagements are keyed by, so the engagement is how an incoming row is resolved to a
/// master. Nothing is written to it.
///
/// <b>Engagement not found</b> — log a warning and carry on rather than holding the file for retry.
/// AS400 mints the id at drawdown, which happens after the appraisal completes and its engagement
/// exists. Note that a full dump (rather than a nightly delta) also contains collateral appraised
/// long before this system existed; those rows can never match and are expected here.
///
/// <b>Block projects are skipped.</b> For a block project AS400 mints an id per unit it financed, so a
/// single project appraisal owns many ids while its master has one slot. Whichever row won would
/// decide the state of the whole project, so one redeemed unit would mark the project redeemed and
/// drop all of its units from the regulatory export. Those rows are therefore left unwritten;
/// <c>collateral.ProjectUnits</c> is where a unit's id belongs (see <c>ProjectUnit.HostCollateralId</c>).
///
/// <b>How a block row actually reaches this class.</b> AS400 packs the project's 8-digit appraisal
/// number and a 2-digit unit sequence into the 10-character CCSURV field, so the value never equals a
/// stored <c>AppraisalNumber</c> and <see cref="LoadEngagementsAsync"/>'s exact match misses. Such rows
/// land in <c>notFound</c>, not in the guard below. The guard remains for a project appraisal number
/// arriving without a sequence. Per-unit ingest is planned but paused; see
/// <c>.claude/tasks/as400-host-collateral-link.md</c>.
/// </summary>
public class HostCollateralLinkIngestor(
    CollateralDbContext dbContext,
    ICollateralMasterRepository repository,
    ILogger<HostCollateralLinkIngestor> logger) : IHostCollateralLinkIngestor
{
    /// <summary>IN-clause chunk size, matching ReappraisalIngestor.</summary>
    private const int BatchSize = 1000;

    public async Task<HostLinkIngestResult> IngestAsync(
        string fileName,
        DateOnly fileDate,
        ParsedHostLinkFile parsed,
        CancellationToken cancellationToken = default)
    {
        var groups = parsed.Records
            .GroupBy(r => r.AppraisalReportNumber, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var records = groups.Values.Select(PickWinningRecord).ToList();

        if (records.Count != parsed.Records.Count)
            logger.LogWarning(
                "[HostCollateralLinkIngestor] Collapsed {Dup} duplicate AppraisalNumber row(s) in {File}",
                parsed.Records.Count - records.Count, fileName);

        // ── Record the feed's own state first, keyed the way the feed keys it ─────────────────
        // This happens BEFORE any master resolution on purpose: it must not depend on a
        // CollateralMaster existing. 6,699 completed appraisals never get one on the production-like
        // dataset, and their AS400 ids used to have nowhere to land — they were reported as NotFound
        // and the id was dropped. Keyed by the feed's own collateral id, nothing else has to have
        // succeeded.
        //
        // NOTE the argument: collapsed by COLLATERAL id, not by appraisal number. The feed is one row
        // per collateral and 952 appraisals on the 2026-08-03 file carry more than one, so collapsing
        // per appraisal is what used to drop 8,383 of 36,110 rows. Collapsing is still required
        // though — one file can restate the same collateral twice (a drawdown and a redemption), and
        // upserting both would hit UX_HostCollateralLinks_HostCollateralId. PickWinningRecord settles
        // it the same way it settles the master's state: newest event date wins, redemption breaks a
        // tie.
        //
        // The appraisal number is stored exactly as AS400 sent it (CCSURV), including the 'B' prefix
        // on block projects. Normalising here would lose what the feed actually said.
        var perCollateral = parsed.Records
            .GroupBy(r => r.HostCollateralId, StringComparer.Ordinal)
            .Select(g => PickWinningRecord([.. g]))
            .ToList();

        await UpsertHostLinksAsync(perCollateral, cancellationToken);

        var engagements = await LoadEngagementsAsync(
            records.Select(r => r.AppraisalReportNumber), cancellationToken);

        // ── Resolve every row to a master, then collapse a second time ────────────────────────
        // One master can be addressed by several appraisal numbers in the same file (a reappraisal
        // and its predecessor both name the same collateral). PickWinningRecord above only settles
        // ties within ONE appraisal number, so without this pass the last row to be iterated would
        // decide the master's state — and AS400 orders its file by collateral id, not by event date,
        // so that order says nothing about which event is the more recent.
        var perMaster = new Dictionary<Guid, List<ParsedHostLinkRecord>>();
        var notFound = new List<string>();
        var projectSkipped = 0;

        foreach (var record in records)
        {
            if (!engagements.TryGetValue(record.AppraisalReportNumber, out var engagement))
            {
                notFound.Add(record.AppraisalReportNumber);
                continue;
            }

            if (engagement.AppraisedCollateralType == CollateralTypes.Project)
            {
                projectSkipped++;
                logger.LogWarning(
                    "[HostCollateralLinkIngestor] AppraisalNumber {Number} in {File} is a block project — "
                    + "not written. AS400 issues one collateral id per financed unit and the file carries no "
                    + "unit key, so the id cannot be attributed. Ids received: {HostCollateralIds}",
                    record.AppraisalReportNumber,
                    fileName,
                    string.Join(", ", groups[record.AppraisalReportNumber]
                        .Select(r => $"{r.HostCollateralId}({r.RecordIndicator})")));
                continue;
            }

            if (!perMaster.TryGetValue(engagement.CollateralMasterId, out var list))
                perMaster[engagement.CollateralMasterId] = list = [];

            list.Add(record);
        }

        var winners = perMaster.ToDictionary(kv => kv.Key, kv => PickWinningRecord(kv.Value));

        var masters = await LoadMastersAsync(winners.Keys, cancellationToken);

        // Aliases are separate CollateralMasters rows standing for the other titles in the same
        // physical group. They hold no engagements, so nothing above reaches them — but a redemption
        // releases every title at once, and leaving them unflagged keeps reporting released titles to
        // the regulator as still held. Loaded in one batch for the whole file.
        var aliasesByParent = (await repository.FindAliasesByParentMasterIdsAsync(
                winners.Keys.ToList(), cancellationToken))
            .GroupBy(a => a.ParentMasterId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        int updated = 0, unchanged = 0;

        foreach (var (masterId, record) in winners)
        {
            if (!masters.TryGetValue(masterId, out var master))
            {
                // The engagement resolved but its master did not — a deleted master, most likely.
                notFound.Add(record.AppraisalReportNumber);
                continue;
            }

            var aliases = aliasesByParent.TryGetValue(masterId, out var found) ? found : [];

            if (IsAlreadyApplied(master, aliases, record))
            {
                unchanged++;
                continue;
            }

            Apply(master, aliases, record);
            updated++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (notFound.Count > 0)
            logger.LogWarning(
                "[HostCollateralLinkIngestor] No CollateralMaster found for {Count} appraisal(s) in "
                + "{File} — skipped. Expected for collateral appraised before this system existed, "
                + "which a full AS400 dump also carries; otherwise it means AppraisalCompletedConsumer "
                + "dead-lettered. AppraisalNumbers: {Numbers}",
                notFound.Count, fileName, string.Join(", ", notFound.Take(50)));

        return new HostLinkIngestResult(
            Received: records.Count,
            Updated: updated,
            Unchanged: unchanged,
            NotFound: notFound.Count,
            ProjectSkipped: projectSkipped);
    }

    /// <summary>
    /// Applies one AS400 row to a master and its aliases. The id lands on the master only; the
    /// aliases take the redemption flags, since AS400 issued one id for the whole group.
    /// </summary>
    private static void Apply(
        CollateralMaster master, List<CollateralMaster> aliases, ParsedHostLinkRecord record)
    {
        var redeeming = record.RecordIndicator == HostLinkRecordIndicators.Redeemed;

        if (redeeming) master.ApplyHostRedemption(record.HostCollateralId, record.RecordDate);
        else           master.ApplyHostDrawdown(record.HostCollateralId);

        // A redemption releases every title in the group; a re-pledge takes them all back. Either
        // way the aliases must end up saying the same thing as their parent, or the titles that are
        // not the IsMaster row drift out of step with it.
        foreach (var alias in aliases)
            alias.SetHostRedemption(redeeming, record.RecordDate);
    }

    /// <summary>True when applying the record would leave every row exactly as it already is.</summary>
    private static bool IsAlreadyApplied(
        CollateralMaster master, List<CollateralMaster> aliases, ParsedHostLinkRecord record)
    {
        var redeeming = record.RecordIndicator == HostLinkRecordIndicators.Redeemed;

        if (master.HostCollateralId != record.HostCollateralId) return false;
        if (master.IsRedeemed != redeeming) return false;
        if (master.RedeemedDate != (redeeming ? record.RecordDate : null)) return false;

        return aliases.All(a =>
            a.IsRedeemed == redeeming &&
            a.RedeemedDate == (redeeming ? record.RecordDate : null));
    }

    /// <summary>
    /// Picks the winning row when several rows describe the same collateral — whether they share an
    /// appraisal number or merely resolve to the same master.
    ///
    /// The decision must be made on <c>RecordDate</c>, not on file order: AS400 orders rows by
    /// collateral id, not by event date, so relying on position could discard a redemption merely
    /// because it happens to precede an older drawdown. On an equal date 'R' beats 'D', because
    /// redemption is the later state in the lifecycle.
    ///
    /// Public so it can be unit-tested without a DbContext.
    /// </summary>
    public static ParsedHostLinkRecord PickWinningRecord(IEnumerable<ParsedHostLinkRecord> sameCollateral)
        => sameCollateral
            .OrderBy(r => r.RecordDate ?? MissingDateRank(r.RecordIndicator))
            .ThenBy(r => r.RecordIndicator == HostLinkRecordIndicators.Redeemed ? 1 : 0)
            .Last();

    /// <summary>
    /// Stand-in date used when AS400 sends a blank or malformed RecordDate
    /// (<c>ParseDdmmyyyyOrNull</c> returns null for "00000000", blanks and out-of-range dates).
    ///
    /// Each side is biased towards its safe direction:
    ///   undated 'R' → treated as most recent, so a redemption is never lost; otherwise released
    ///                 collateral would keep being reported to the regulator as still held.
    ///   undated 'D' → treated as oldest, so a corrupt old drawdown cannot mask a clearly dated
    ///                 redemption.
    /// </summary>
    private static DateOnly MissingDateRank(string indicator)
        => indicator == HostLinkRecordIndicators.Redeemed ? DateOnly.MaxValue : DateOnly.MinValue;

    /// <summary>
    /// Resolves appraisal numbers to their engagements. Read-only — the engagement is only the route
    /// from the file's key to a master — so it is not tracked.
    /// </summary>
    /// <summary>
    /// Inserts or refreshes one <see cref="HostCollateralLink"/> per appraisal number in the file.
    /// Rows the feed no longer mentions are left alone — a full dump is the authority on what it
    /// contains, not on what it omits, and deleting on absence would drop collateral the bank still
    /// holds the moment AS400 sends a partial file.
    /// </summary>
    private async Task UpsertHostLinksAsync(
        List<ParsedHostLinkRecord> records,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now;

        foreach (var chunk in records.Chunk(BatchSize))
        {
            var ids = chunk.Select(r => r.HostCollateralId).ToList();

            var existing = await dbContext.HostCollateralLinks
                .Where(h => ids.Contains(h.HostCollateralId))
                .ToDictionaryAsync(h => h.HostCollateralId, StringComparer.Ordinal, cancellationToken);

            foreach (var record in chunk)
            {
                var values = new HostCollateralLinkValues(
                    AppraisalNumber: record.AppraisalReportNumber,
                    CollateralName: record.CollateralName,
                    IsRedeemed: record.RecordIndicator == HostLinkRecordIndicators.Redeemed,
                    MasterTitle: record.MasterTitle,
                    LocationCode: record.LocationCode,
                    CollateralCode: record.CollateralCode,
                    PropertyType: record.PropertyType,
                    PropertyTypeDesc: record.PropertyTypeDesc,
                    RecordDate: record.RecordDate);

                if (existing.TryGetValue(record.HostCollateralId, out var link))
                {
                    if (link.Matches(values)) continue;
                    link.Apply(values, now);
                }
                else
                {
                    dbContext.HostCollateralLinks.Add(
                        new HostCollateralLink(record.HostCollateralId, values, now));
                }
            }
        }
    }

    private async Task<Dictionary<string, EngagementRef>> LoadEngagementsAsync(
        IEnumerable<string> appraisalNumbers,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, EngagementRef>(StringComparer.Ordinal);

        foreach (var chunk in appraisalNumbers.Distinct(StringComparer.Ordinal).Chunk(BatchSize))
        {
            var rows = await dbContext.CollateralEngagements
                .AsNoTracking()
                .Where(e => chunk.Contains(e.AppraisalNumber))
                .Select(e => new EngagementRef(
                    e.AppraisalNumber, e.CollateralMasterId, e.AppraisedCollateralType))
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                // AppraisalNumber should be unique; a collision means bad upstream data worth surfacing.
                if (result.ContainsKey(row.AppraisalNumber))
                    logger.LogWarning(
                        "[HostCollateralLinkIngestor] More than one engagement found for "
                        + "AppraisalNumber {Number} — using the last one", row.AppraisalNumber);

                result[row.AppraisalNumber] = row;
            }
        }

        return result;
    }

    /// <summary>Loads the masters to be written, tracked so that mutations are persisted.</summary>
    private async Task<Dictionary<Guid, CollateralMaster>> LoadMastersAsync(
        IEnumerable<Guid> masterIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, CollateralMaster>();

        foreach (var chunk in masterIds.Distinct().Chunk(BatchSize))
        {
            var rows = await dbContext.CollateralMasters
                .Where(m => chunk.Contains(m.Id))
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
                result[row.Id] = row;
        }

        return result;
    }

    /// <summary>The only three engagement columns this class reads.</summary>
    private sealed record EngagementRef(
        string AppraisalNumber, Guid CollateralMasterId, string? AppraisedCollateralType);
}
