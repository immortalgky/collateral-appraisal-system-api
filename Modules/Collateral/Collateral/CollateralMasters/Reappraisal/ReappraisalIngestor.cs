using Collateral.Contracts.Reappraisal;
using Collateral.Data;
using Dapper;
using Integration.Contracts.Reappraisal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Time;

namespace Collateral.CollateralMasters.Reappraisal;

/// <summary>
/// Upserts reappraisal candidates from a parsed COLLATREV file into the Collateral data store.
///
/// <b>One row per book, not per file.</b> AS400 does not know which books CAS has already reviewed, so
/// every monthly file repeats the ones still on its due list. A book is (CollateralId, normalised survey
/// number), looked up across ALL earlier files:
///   - new book            → new Pending row
///   - Pending / Deleted   → refreshed with this file's values, status kept ("not reviewing this
///                           round" stays until staff restore it)
///   - Consumed            → skipped outright, not even refreshed — the book has been reviewed —
///                           unless the reappraisal it produced was cancelled, which puts it back to
///                           Pending and refreshes it
///   - seen in a NEWER file already → skipped, so a late backlog file cannot roll a book back
/// A Pending or Deleted book missing from the latest file drops off the list by its LastSeenFileDate;
/// nothing here deletes it.
///
/// <b>Block-project units.</b> A number that names a block-project appraisal — with or without AS400's
/// 'B', or a unit ticket — is one UNIT of the project, and each collateral listing it is a different
/// unit. Those rows are flagged <see cref="ReappraisalCandidate.IsBlockUnit"/> and reviewed per collateral:
/// "reviewed elsewhere" and "still under review" look only at the same collateral. A ticket is stored
/// under its project's appraisal number, so a unit sent as "B…" one month and as a ticket the next is
/// one row.
/// </summary>
public class ReappraisalIngestor(
    CollateralDbContext dbContext,
    ISqlConnectionFactory connectionFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<ReappraisalIngestor> logger) : IReappraisalIngestor
{
    /// <summary>IN-clause chunk size, under SQL Server's 2100-parameter cap.</summary>
    private const int BatchSize = 1000;

    public async Task IngestAsync(
        string fileName,
        DateOnly fileDate,
        ParsedReappraisalFile parsed,
        CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.ApplicationNow;
        var books = await ResolveBooksAsync(parsed.Details.Select(d => d.SurveyNumber), cancellationToken);
        (string CollateralId, string Number) KeyOf(ParsedDetailRecord d) => (d.CollateralId, books[As400AppraisalNumber.Normalize(d.SurveyNumber)].Book);

        // One context serves every file of a run, and books repeat from file to file: rows attached
        // for the previous file (already saved) would clash with this file's fresh copies.
        dbContext.ChangeTracker.Clear();

        // Collapse duplicate books within a single file before upsert — a COLLATREV file occasionally
        // repeats a row. Keep the last occurrence (latest wins).
        var details = parsed.Details
            .GroupBy(KeyOf)
            .Select(g => g.Last())
            .ToList();
        if (details.Count != parsed.Details.Count)
            logger.LogWarning("[ReappraisalIngestor] Collapsed {Dup} duplicate book row(s) in {File}",
                parsed.Details.Count - details.Count, fileName);

        var (existing, spellings) = await LoadBooksAsync(details.Select(d => d.CollateralId), cancellationToken);
        var keys = details.ToDictionary(d => d, KeyOf);
        var blockBooks = books.Values.Where(b => b.IsBlockUnit).Select(b => b.Book).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var reviewedElsewhere = await FindReviewedBooksAsync(keys.Values, existing, blockBooks, cancellationToken);
        var reopenable = await FindReopenableAsync(keys.Values, existing, reviewedElsewhere, blockBooks, cancellationToken);

        // Rows that need lat/lon enrichment (new or refreshed). Only these — every loaded row is tracked,
        // and a reviewed book must not be touched at all.
        var needsEnrichment = new List<ReappraisalCandidate>();
        int created = 0, refreshed = 0, reopened = 0, skippedReviewed = 0, skippedOlder = 0;

        foreach (var detail in details)
        {
            var key = keys[detail];
            var isBlockUnit = blockBooks.Contains(key.Number);

            if (!existing.TryGetValue(key, out var book))
            {
                var candidate = ReappraisalCandidate.Create(
                    fileName,
                    fileDate,
                    parsed.EffectiveDate,
                    now,
                    detail.RowHash,
                    detail.ReviewType,
                    detail.ReviewDate,
                    detail.CollateralId,
                    detail.SurveyNumber,
                    detail.CollateralCode,
                    detail.CollateralCategory,
                    detail.CollateralName,
                    detail.CollateralAddress,
                    detail.CifNumber,
                    detail.CifName,
                    detail.AoCode,
                    detail.AoName,
                    detail.TitleNumber,
                    detail.CurrentValue,
                    detail.ValuationDate,
                    detail.InternalExternal,
                    detail.BusinessSize,
                    detail.BusinessSizeDesc,
                    detail.MortgageAmount,
                    detail.PastDueDay,
                    detail.ApplicationNumber,
                    detail.FacilityCode,
                    detail.FacilitySequence,
                    detail.CpNumber,
                    detail.CarCode,
                    detail.FacilityLimit,
                    detail.FlagLessAge4Y,
                    detail.FlagGreaterAge4Y,
                    detail.CountAgeingDate,
                    detail.CollateralDescription,
                    detail.ExternalValuerName,
                    detail.InternalValuerName,
                    detail.SllOver100M,
                    detail.SllDescription,
                    detail.Stage,
                    detail.IBGRetail,
                    detail.Group,
                    detail.EffectiveDateAppraisal);
                candidate.SetBook(key.Number, isBlockUnit, detail.SurveyNumber);

                // A book already reviewed under another collateral is the same book — one request
                // covers all of its collateral — so this listing arrives reviewed too, unless that
                // review was cancelled (then it is to-do like any reopened book).
                if (reviewedElsewhere.Contains(key.Number) && !reopenable.Contains(key))
                    candidate.MarkConsumed();

                await dbContext.ReappraisalCandidates.AddAsync(candidate, cancellationToken);
                existing[key] = candidate;
                needsEnrichment.Add(candidate);
                created++;
                continue;
            }

            if (fileDate < (book.LastSeenFileDate ?? book.SourceFileDate))
            {
                skippedOlder++;
                continue;
            }

            if (book.Status == ReappraisalCandidateStatus.Consumed)
            {
                if (!reopenable.Contains(key))
                {
                    skippedReviewed++;
                    continue;
                }

                book.MarkPending();
                reopened++;
            }

            book.MarkSeen(fileDate);
            // The raw number as this file spelled it — unless another (older, per-file) row of the
            // collateral already holds that spelling for the same file date: UX (SourceFileDate,
            // CollateralId, SurveyNumber) would reject the save, failing the whole file.
            var spelling = spellings.Contains((book.SourceFileDate, book.CollateralId, detail.SurveyNumber)) && book.SurveyNumber != detail.SurveyNumber
                ? book.SurveyNumber
                : detail.SurveyNumber;
            book.SetBook(key.Number, isBlockUnit, spelling);

            if (book.RowHash == detail.RowHash)
            {
                // Rows from before projects had a coordinate source would otherwise stay blank (and
                // out of nearby results) until AS400 changed something on the row.
                if (book.Latitude is null)
                    needsEnrichment.Add(book);
                continue;
            }

            book.UpdateFrom(
                detail.RowHash,
                parsed.EffectiveDate,
                detail.ReviewType,
                detail.ReviewDate,
                detail.CollateralCode,
                detail.CollateralCategory,
                detail.CollateralName,
                detail.CollateralAddress,
                detail.CifName,
                detail.AoCode,
                detail.AoName,
                detail.TitleNumber,
                detail.CurrentValue,
                detail.ValuationDate,
                detail.InternalExternal,
                detail.BusinessSize,
                detail.BusinessSizeDesc,
                detail.MortgageAmount,
                detail.PastDueDay,
                detail.ApplicationNumber,
                detail.FacilityCode,
                detail.FacilitySequence,
                detail.CpNumber,
                detail.CarCode,
                detail.FacilityLimit,
                detail.FlagLessAge4Y,
                detail.FlagGreaterAge4Y,
                detail.CountAgeingDate,
                detail.CollateralDescription,
                detail.ExternalValuerName,
                detail.InternalValuerName,
                detail.SllOver100M,
                detail.SllDescription,
                detail.Stage,
                detail.IBGRetail,
                detail.Group,
                detail.EffectiveDateAppraisal);

            needsEnrichment.Add(book);
            refreshed++;
        }

        // Lat/lon enrichment — cross-schema Dapper read-only.
        if (needsEnrichment.Count > 0)
        {
            var coords = await FetchCoordinatesAsync(
                needsEnrichment.Select(c => c.NormalizedSurveyNumber!).ToList(), cancellationToken);

            foreach (var candidate in needsEnrichment)
            {
                if (coords.TryGetValue(candidate.NormalizedSurveyNumber!, out var coord))
                    candidate.SetCoordinates(coord.Latitude, coord.Longitude);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "[ReappraisalIngestor] {File}: {Count} book(s) — {Created} new, {Refreshed} refreshed, " +
            "{Reopened} reopened after a cancelled reappraisal, {Reviewed} already reviewed (skipped), " +
            "{Older} already seen in a newer file (skipped)",
            fileName, details.Count, created, refreshed, reopened, skippedReviewed, skippedOlder);
    }

    /// <summary>
    /// The book each number in the file names, and whether it is a block-project unit: a unit ticket
    /// resolves to the appraisal it was issued from; any number naming a block-project appraisal is a unit.
    /// Keyed by the normalised number.
    /// </summary>
    private async Task<Dictionary<string, (string Book, bool IsBlockUnit)>> ResolveBooksAsync(
        IEnumerable<string> surveyNumbers,
        CancellationToken cancellationToken)
    {
        var numbers = surveyNumbers.Select(As400AppraisalNumber.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        const string ticketSql = """
            SELECT t.TicketNumber, a.AppraisalNumber
            FROM appraisal.UnitTickets t
            JOIN appraisal.Appraisals a ON a.Id = t.AppraisalId AND a.IsDeleted = 0
            WHERE t.TicketNumber IN @Numbers
            """;
        const string blockSql = """
            SELECT DISTINCT a.AppraisalNumber
            FROM appraisal.Appraisals a
            JOIN appraisal.Projects p ON p.AppraisalId = a.Id
            WHERE a.AppraisalNumber IN @Numbers
              AND a.IsDeleted = 0
            """;

        var connection = connectionFactory.GetOpenConnection();
        var tickets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in numbers.Where(As400AppraisalNumber.IsUnitTicket).Chunk(BatchSize))
            foreach (var row in await connection.QueryAsync<(string TicketNumber, string AppraisalNumber)>(ticketSql, new { Numbers = chunk }))
                tickets[row.TicketNumber] = row.AppraisalNumber;

        var resolved = numbers.ToDictionary(n => n, n => tickets.GetValueOrDefault(n) ?? n, StringComparer.OrdinalIgnoreCase);
        var block = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in resolved.Values.Distinct(StringComparer.OrdinalIgnoreCase).Chunk(BatchSize))
            block.UnionWith(await connection.QueryAsync<string>(blockSql, new { Numbers = chunk }));
        cancellationToken.ThrowIfCancellationRequested();

        return resolved.ToDictionary(
            kv => kv.Key,
            kv => (kv.Value, tickets.ContainsKey(kv.Key) || block.Contains(kv.Value)),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Every existing row for the file's collaterals, one per book. Rows written before books were
    /// deduplicated can repeat a book across months; the most recently seen copy wins, a Consumed one on
    /// a tie. Recency first is what keeps a reopened book reopened (the reopened row is the one stamped)
    /// and a newer Pending copy ahead of an old month's Deleted one (old "delete" meant that month
    /// only). Stale Pending/Deleted copies of a reviewed book were consumed once by
    /// 20260928120000_DataFix_BackfillReappraisalCandidateBookKey.sql, so recency never hands a
    /// reviewed book back to the to-do list. Losers keep an old LastSeenFileDate and stay hidden.
    /// </summary>
    private async Task<(Dictionary<(string CollateralId, string Number), ReappraisalCandidate> Books,
        HashSet<(DateOnly, string, string)> Spellings)> LoadBooksAsync(
        IEnumerable<string> collateralIds,
        CancellationToken cancellationToken)
    {
        var rows = new List<ReappraisalCandidate>();
        foreach (var chunk in collateralIds.Distinct().Chunk(BatchSize))
            rows.AddRange(await dbContext.ReappraisalCandidates
                .AsNoTracking()
                .Where(c => chunk.Contains(c.CollateralId))
                .ToListAsync(cancellationToken));

        // Loaded untracked: history can hold many old copies per book, and only the winner is ever
        // changed — tracking the losers would only make every DetectChanges scan them.
        var books = rows
            .GroupBy(c => (c.CollateralId, Number: c.NormalizedSurveyNumber ?? As400AppraisalNumber.Normalize(c.SurveyNumber)))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(c => c.LastSeenFileDate ?? c.SourceFileDate)
                    .ThenBy(c => c.Status == ReappraisalCandidateStatus.Consumed ? 0 : 1)
                    .First());

        dbContext.ReappraisalCandidates.AttachRange(books.Values);
        return (books, rows.Select(c => (c.SourceFileDate, c.CollateralId, c.SurveyNumber)).ToHashSet());
    }

    /// <summary>
    /// Book numbers in this file that are new to their collateral but already reviewed (Consumed) under
    /// another collateral. Loading is per collateral, so without this a book listed under a new
    /// collateral would look brand new and could be reviewed a second time. Not for block-project units:
    /// another collateral of the same project is another unit, reviewed on its own.
    /// </summary>
    private async Task<HashSet<string>> FindReviewedBooksAsync(
        IEnumerable<(string CollateralId, string Number)> keys,
        IReadOnlyDictionary<(string CollateralId, string Number), ReappraisalCandidate> existing,
        IReadOnlySet<string> blockBooks,
        CancellationToken cancellationToken)
    {
        var newNumbers = keys
            .Where(k => !existing.ContainsKey(k) && !blockBooks.Contains(k.Number))
            .Select(k => k.Number)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var reviewed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in newNumbers.Chunk(BatchSize))
            reviewed.UnionWith(await dbContext.ReappraisalCandidates
                .AsNoTracking()
                .Where(c => chunk.Contains(c.NormalizedSurveyNumber!)
                            && c.Status == ReappraisalCandidateStatus.Consumed)
                .Select(c => c.NormalizedSurveyNumber!)
                .Distinct()
                .ToListAsync(cancellationToken));

        return reviewed;
    }

    /// <summary>
    /// Consumed books in this file that nothing is reviewing any more: no non-cancelled reappraisal
    /// points back at the book (by id or, for a legacy AS400 book, by number) and no request for the
    /// book is waiting — unsubmitted, or submitted with its appraisal not created yet. Appraisal is where cancellation lives — Request has no
    /// cancelled state of its own.
    /// </summary>
    private async Task<HashSet<(string CollateralId, string Number)>> FindReopenableAsync(
        IEnumerable<(string CollateralId, string Number)> keys,
        IReadOnlyDictionary<(string CollateralId, string Number), ReappraisalCandidate> existing,
        IReadOnlySet<string> reviewedElsewhere,
        IReadOnlySet<string> blockBooks,
        CancellationToken cancellationToken)
    {
        // Reviewed books in this file: a Consumed row under this collateral, or — for a collateral
        // listing the book for the first time — a Consumed row under another one.
        var consumed = keys
            .Where(k => existing.TryGetValue(k, out var c)
                ? c.Status == ReappraisalCandidateStatus.Consumed
                : reviewedElsewhere.Contains(k.Number))
            .ToList();

        if (consumed.Count == 0)
            return [];

        // Still standing for the book: any reappraisal not cancelled (appraisal.vw_ReappraisalsByBook —
        // every way an appraisal reappraises a book), or a request for it still waiting to enter the
        // workflow. With the collateral it was raised for, when known: a block-project unit only counts
        // what was raised for its own collateral. One round trip.
        const string liveSql = """
            SELECT rb.BookNumber, rb.CollateralId
            FROM appraisal.vw_ReappraisalsByBook rb
            WHERE rb.BookNumber IN @Numbers
              AND rb.Status <> 'Cancelled'
            UNION
            SELECT w.BookNumber, w.CollateralId
            FROM request.vw_WaitingReappraisalRequests w
            WHERE w.BookNumber IN @Numbers
            """;

        // A live reappraisal raised by the OLD Initiate, which marked the request ExternalSystem "SIBS"
        // with the collateral id as ExternalCaseKey (the current one writes neither). liveSql cannot see
        // two kinds: a book not in CAS (it got a Guid.Empty PrevAppraisalId), and a block-project unit (no
        // request named its collateral; ReappraisalCollateralId came later) — without this, the first file
        // after deploy would reopen them while still under review. Reappraisal purposes only, so an
        // LOS-channel progressive or appeal request is never mistaken for one.
        // ponytail: collateral-grain, so such a reappraisal also keeps a different book of the same
        // collateral from reopening; drop this query once pre-change rows have aged out.
        const string oldFlowSql = """
            SELECT DISTINCT r.ExternalCaseKey
            FROM request.Requests r
            JOIN appraisal.Appraisals a ON a.RequestId = r.Id
            WHERE r.ExternalSystem = 'SIBS'
              AND r.ExternalCaseKey IN @CollateralIds
              AND r.IsDeleted = 0
              AND a.Purpose IN ('03', '09')
              AND a.Status <> 'Cancelled'
              AND a.IsDeleted = 0
              -- Only where liveSql is blind: the prior book is not an appraisal in CAS, or it is a block
              -- project (a unit). Any other old reappraisal is in liveSql already — and counted here a
              -- completed one would keep every later book of the collateral from ever reopening.
              AND (NOT EXISTS (SELECT 1 FROM appraisal.Appraisals p WHERE p.Id = a.PrevAppraisalId)
                   OR EXISTS (SELECT 1 FROM appraisal.Projects pj WHERE pj.AppraisalId = a.PrevAppraisalId))
            """;

        var connection = connectionFactory.GetOpenConnection();
        var liveBooks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var liveUnits = new HashSet<(string, string)>();
        var oldFlow = new HashSet<string>(StringComparer.Ordinal);

        foreach (var chunk in consumed.Select(k => k.Number).Distinct().Chunk(BatchSize))
            foreach (var row in await connection.QueryAsync<(string BookNumber, string? CollateralId)>(liveSql, new { Numbers = chunk }))
            {
                liveBooks.Add(row.BookNumber);
                if (row.CollateralId is not null)
                    liveUnits.Add((row.CollateralId, row.BookNumber.ToUpperInvariant()));
            }

        foreach (var chunk in consumed.Select(k => k.CollateralId).Distinct().Chunk(BatchSize))
            oldFlow.UnionWith(await connection.QueryAsync<string>(oldFlowSql, new { CollateralIds = chunk }));

        cancellationToken.ThrowIfCancellationRequested();

        bool IsLive((string CollateralId, string Number) k) => blockBooks.Contains(k.Number)
            ? liveUnits.Contains((k.CollateralId, k.Number.ToUpperInvariant()))
            : liveBooks.Contains(k.Number);

        return consumed
            .Where(k => !IsLive(k) && !oldFlow.Contains(k.CollateralId))
            .ToHashSet();
    }

    /// <summary>
    /// Cross-schema Dapper query: looks up the best available coordinates for each normalised survey
    /// number (= AppraisalNumber). Checks both Land and Condo detail tables (union).
    /// </summary>
    private async Task<Dictionary<string, (decimal Latitude, decimal Longitude)>> FetchCoordinatesAsync(
        IReadOnlyList<string> surveyNumbers,
        CancellationToken cancellationToken)
    {
        if (surveyNumbers.Count == 0)
            return new Dictionary<string, (decimal, decimal)>();

        const string sql = """
            SELECT a.AppraisalNumber AS SurveyNumber,
                   CAST(d.Latitude AS decimal(10,7)) AS Latitude,
                   CAST(d.Longitude AS decimal(10,7)) AS Longitude
            FROM appraisal.Appraisals a
            CROSS APPLY (
                SELECT TOP 1 u.Latitude, u.Longitude
                FROM (
                    SELECT TOP 1 ld.Latitude, ld.Longitude, 1 AS Pref
                    FROM appraisal.LandAppraisalDetails ld
                    JOIN appraisal.AppraisalProperties ap ON ap.Id = ld.AppraisalPropertyId
                    WHERE ap.AppraisalId = a.Id AND ld.Latitude IS NOT NULL AND ld.Longitude IS NOT NULL
                    UNION ALL
                    SELECT TOP 1 cd.Latitude, cd.Longitude, 2 AS Pref
                    FROM appraisal.CondoAppraisalDetails cd
                    JOIN appraisal.AppraisalProperties ap ON ap.Id = cd.AppraisalPropertyId
                    WHERE ap.AppraisalId = a.Id AND cd.Latitude IS NOT NULL AND cd.Longitude IS NOT NULL
                    UNION ALL
                    -- A block project has no properties; its units sit at the project's location.
                    SELECT TOP 1 pj.Latitude, pj.Longitude, 3 AS Pref
                    FROM appraisal.Projects pj
                    WHERE pj.AppraisalId = a.Id AND pj.Latitude IS NOT NULL AND pj.Longitude IS NOT NULL
                ) u
                ORDER BY u.Pref
            ) d
            WHERE a.AppraisalNumber IN @SurveyNumbers
            """;

        // Batch the IN list to stay under SQL Server's 2100-parameter cap on large monthly/backfill files.
        var distinct = surveyNumbers.Distinct().ToList();
        var result = new Dictionary<string, (decimal Latitude, decimal Longitude)>();
        var connection = connectionFactory.GetOpenConnection();

        for (var i = 0; i < distinct.Count; i += BatchSize)
        {
            var batch = distinct.GetRange(i, Math.Min(BatchSize, distinct.Count - i));
            var rows = await connection.QueryAsync<CoordinateRow>(sql, new { SurveyNumbers = batch });
            foreach (var g in rows.GroupBy(r => r.SurveyNumber))
                result[g.Key] = (g.First().Latitude, g.First().Longitude);
        }

        return result;
    }

    private sealed record CoordinateRow(string SurveyNumber, decimal Latitude, decimal Longitude);
}
