using Collateral.CollateralMasters.Reappraisal;
using Collateral.CollateralMasters.Reappraisal.Services;
using Dapper;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;
using Request.Contracts.Requests.Dtos;

namespace Collateral.Application.Features.Reappraisal.InitiateReappraisal;

/// <summary>
/// Handles <see cref="InitiateReappraisalCommand"/>.
///
/// Collateral-side responsibilities (runs atomically in one CollateralDbContext transaction):
///   1. Load Pending candidates.
///   2. Resolve the normalised survey number ('B' prefix dropped) → AppraisalId via Dapper.
///   3. Layer 1 dedupe — skip a book whose reappraisal is already under way: an open Appraisal points
///      back at it (by id, or by number for a legacy book), or a Request for the book is still
///      waiting (request.vw_WaitingReappraisalRequests), or the book is already in this batch; and a
///      nearby book already reappraised to completion. A candidate not on AS400's latest file is NotDue.
///   4. Generate one shared group number.
///   5. Publish one <see cref="ReappraisalInitiatedIntegrationEvent"/> per working item via the
///      Collateral outbox. A book not in CAS (legacy AS400 "99A…") is published too, carrying its
///      number — and the value/date from the bank's listing, when it has one — instead of a PrevAppraisalId.
///   6. SaveChanges (outbox messages in one transaction).
///
/// Candidates are NOT consumed here: Initiate only creates requests, and a book counts as reviewed
/// once its request is submitted (<c>RequestSubmittedReappraisalConsumer</c>). Until then the waiting
/// request keeps it from being initiated twice.
///
/// Request-side work (async, handled by <c>ReappraisalInitiatedIntegrationEventHandler</c>):
///   Creates one reappraisal Request per event message and leaves it for staff to submit.
///
/// Return: <see cref="InitiateReappraisalResult"/> with GroupNumber + accepted count.
/// CreatedRequestIds are NOT returned — they are created asynchronously by the consumer.
/// The FE should navigate to the reappraisal list filtered by GroupNumber.
/// </summary>
public class InitiateReappraisalCommandHandler(
    CollateralDbContext dbContext,
    IReappraisalGroupNumberGenerator groupNumberGenerator,
    ISqlConnectionFactory connectionFactory,
    IIntegrationEventOutbox outbox,
    ILogger<InitiateReappraisalCommandHandler> logger
) : ICommandHandler<InitiateReappraisalCommand, InitiateReappraisalResult>
{
    public async Task<InitiateReappraisalResult> Handle(
        InitiateReappraisalCommand command,
        CancellationToken cancellationToken)
    {
        var hasCandidates = command.CandidateIds.Count > 0;
        var hasNearby     = command.NearbyAppraisalIds.Count > 0;

        if (!hasCandidates && !hasNearby)
            throw new ArgumentException(
                "At least one CandidateId or NearbyAppraisalId must be provided.", nameof(command));

        var skipped = new List<SkippedReappraisalItem>();

        // ── Step 1: Load Pending candidates (CandidateIds path) ──────────────
        // Only books on AS400's latest file — the same rule as the list (an old copy, or a book that has
        // dropped off the file, is no longer due; a stale tab or direct call could still send its id).
        // Those are reported back as NotDue rather than dropped silently.
        var pending = hasCandidates
            ? await dbContext.ReappraisalCandidates
                .Where(c => command.CandidateIds.Contains(c.Id)
                            && c.Status == ReappraisalCandidateStatus.Pending)
                .ToListAsync(cancellationToken)
            : new List<ReappraisalCandidate>();
        if (pending.Count == 0 && !hasNearby)
            throw new InvalidOperationException("No Pending candidates found for the provided CandidateIds.");

        // ── Step 2: Resolve the book number → PrevAppraisalId (for the NotDue skips too) ─
        var surveyNumbers       = pending.Select(BookNumber).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var appraisalIdBySurvey = await ResolvePrevAppraisalIdsAsync(surveyNumbers, cancellationToken);
        Guid? PrevId(string book) => appraisalIdBySurvey.TryGetValue(book, out var id) ? id : null;

        var dueIds = await FindDueCandidateIdsAsync(pending.Select(c => c.Id).ToList(), cancellationToken);
        var candidates = pending.Where(c => dueIds.Contains(c.Id)).ToList();
        skipped.AddRange(pending
            .Where(c => !dueIds.Contains(c.Id))
            .Select(c => new SkippedReappraisalItem(PrevId(BookNumber(c)), BookNumber(c), "NotDue")));

        if (hasCandidates && candidates.Count != command.CandidateIds.Count)
        {
            var found   = candidates.Select(c => c.Id).ToHashSet();
            var missing = command.CandidateIds.Where(id => !found.Contains(id)).ToList();
            logger.LogWarning(
                "[REAPPRAISAL-INITIATE] {Count} candidates not found, not Pending, or not on the latest file: {Ids}",
                missing.Count, string.Join(", ", missing));
        }

        // ── Step 3: Build working list ─────────────────────────────────────────
        var workingItems = candidates
            .Select(c => new WorkingItem(PrevId(BookNumber(c)), BookNumber(c), c, FromNearby: false))
            .ToList();

        // NearbyAppraisalIds path: fetch appraisal numbers, find any matching Pending candidate
        if (hasNearby)
        {
            var nearbyRows = await FetchAppraisalNumbersAsync(command.NearbyAppraisalIds, cancellationToken);

            var candidateBySurvey = candidates
                .GroupBy(BookNumber, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Pending candidates for the other nearby books: only copies on AS400's latest file (the
            // same rule as the CandidateIds path), the most recently listed one per book.
            var otherNumbers = nearbyRows
                .Select(r => r.AppraisalNumber)
                .OfType<string>()
                .Where(n => !candidateBySurvey.ContainsKey(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (otherNumbers.Count > 0)
            {
                // EF sends the list as one JSON parameter — no 2,100-parameter cap to chunk around.
                var others = await dbContext.ReappraisalCandidates
                    .Where(c => otherNumbers.Contains(c.NormalizedSurveyNumber!)
                                && c.Status == ReappraisalCandidateStatus.Pending)
                    .ToListAsync(cancellationToken);
                var othersDue = await FindDueCandidateIdsAsync(others.Select(c => c.Id).ToList(), cancellationToken);
                others.RemoveAll(c => !othersDue.Contains(c.Id));
                foreach (var g in others.GroupBy(c => c.NormalizedSurveyNumber!, StringComparer.OrdinalIgnoreCase))
                    candidateBySurvey[g.Key] = g.OrderByDescending(c => c.LastSeenFileDate ?? c.SourceFileDate).First();
            }

            foreach (var row in nearbyRows)
            {
                if (workingItems.Any(w => w.AppraisalId == row.AppraisalId))
                    continue;

                ReappraisalCandidate? matchedCandidate = null;
                if (!string.IsNullOrWhiteSpace(row.AppraisalNumber))
                    candidateBySurvey.TryGetValue(row.AppraisalNumber, out matchedCandidate);

                workingItems.Add(new WorkingItem(row.AppraisalId, row.AppraisalNumber, matchedCandidate, FromNearby: true));
            }
        }

        // ── Step 4: Layer 1 dedupe ─────────────────────────────────────────────
        // A book is reviewed once, whichever collateral it is listed under: the request copies the prior
        // request's titles, so it covers every collateral of the book — the same grain as consumption on
        // submit. Under review = an open reappraisal of the book, or a request for it still waiting.
        // A nearby book is also refused once any reappraisal of it has completed — the nearby grid hides
        // those (a completed reappraisal supersedes the book), so only a stale tab or a direct call can
        // send one, and initiating it would fork the chain from the superseded book.
        // A block-project unit is the exception to "once per book": each collateral is a different unit,
        // so its checks and the in-batch dedupe are per collateral, against reappraisals raised for it.
        var bookNumbers = workingItems
            .Select(w => w.AppraisalNumber)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var reviewRows = await FindBooksUnderReviewAsync(bookNumbers, cancellationToken);
        bool Matches(BookReviewRow r, WorkingItem item) =>
            string.Equals(r.BookNumber, item.AppraisalNumber, StringComparison.OrdinalIgnoreCase)
            && (item.Candidate is not { IsBlockUnit: true } || r.CollateralId == item.Candidate.CollateralId);
        var batchBooks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var toProcess = new List<WorkingItem>();

        foreach (var item in workingItems)
        {
            // Every request must carry its book (the key for waiting / in-progress checks and for
            // consumption on submit); the consumer refuses a message without one. Say so here instead
            // of reporting a request that will never be created.
            if (string.IsNullOrWhiteSpace(item.AppraisalNumber))
            {
                skipped.Add(new SkippedReappraisalItem(item.AppraisalId, item.AppraisalNumber, "NoBookNumber"));
                continue;
            }

            // The same book selected twice in this batch (listed under two collateral): one request —
            // unless it is a block-project unit, where each collateral is its own.
            var batchKey = item.Candidate is { IsBlockUnit: true } c
                ? $"{item.AppraisalNumber}|{c.CollateralId}"
                : item.AppraisalNumber;
            var reason = reviewRows.Any(r => r.IsOpen && Matches(r, item)) ? "AlreadyInFlight"
                : item.FromNearby && reviewRows.Any(r => !r.IsOpen && Matches(r, item)) ? "AlreadyReviewed"
                : !batchBooks.Add(batchKey) ? "AlreadyInFlight"
                : null;

            if (reason is null)
            {
                toProcess.Add(item);
                continue;
            }

            skipped.Add(new SkippedReappraisalItem(item.AppraisalId, item.AppraisalNumber, reason));
            logger.LogInformation(
                "[REAPPRAISAL-INITIATE] Skipped {Number} (AppraisalId {AppraisalId}) — {Reason}",
                item.AppraisalNumber, item.AppraisalId, reason);
        }

        if (toProcess.Count == 0)
        {
            logger.LogWarning(
                "[REAPPRAISAL-INITIATE] All items skipped — no events published for group {GroupNumber}.",
                "pending-generation");
            // Still generate a group number so the FE gets a consistent response.
            var noOpGroup = await groupNumberGenerator.GenerateAsync(cancellationToken);
            return new InitiateReappraisalResult(noOpGroup, [], skipped);
        }

        // ── Step 5: Generate shared group number ──────────────────────────────
        var groupNumber = await groupNumberGenerator.GenerateAsync(cancellationToken);

        // ── Step 6: Publish outbox events (candidates are consumed on submit, not here) ──
        var units = await FindUnitsAsync(
            toProcess.Select(i => i.Candidate).OfType<ReappraisalCandidate>().Where(c => c.IsBlockUnit).Select(c => c.Id).ToList(),
            cancellationToken);
        // A book with no appraisal in CAS (legacy AS400 "99A…") has only its number — and the prior
        // value/date from the bank's listing — to carry into the request. XOR with PrevAppraisalId.
        // Every item here has a book number (NoBookNumber was skipped above).
        static bool IsLegacy(WorkingItem i) => !i.AppraisalId.HasValue && i.Candidate is not null;
        var legacyPriors = await FindLegacyPriorsAsync(
            toProcess.Where(IsLegacy).Select(i => i.AppraisalNumber!).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            cancellationToken);
        foreach (var item in toProcess)
        {
            var legacy = IsLegacy(item);
            var legacyPrior = legacy ? legacyPriors.GetValueOrDefault(item.AppraisalNumber!) : null;

            outbox.Publish(new ReappraisalInitiatedIntegrationEvent
            {
                GroupNumber    = groupNumber,
                Source         = item.Candidate is not null ? "Candidate" : "InSystem",
                CandidateId    = item.Candidate?.Id,
                // The book number as CAS stores it ('B' prefix dropped) — the key the waiting-request
                // check and the candidate list match on.
                SurveyNumber   = item.AppraisalNumber,
                CifNumber      = item.Candidate?.CifNumber,
                CifName        = item.Candidate?.CifName,
                CollateralId   = item.Candidate?.CollateralId,
                PrevAppraisalId = item.AppraisalId,
                PrevAppraisalNumber = legacy ? item.AppraisalNumber : null,
                PrevAppraisalValue  = legacyPrior?.Value,
                PrevAppraisalDate   = legacyPrior?.Date,
                IsBlockUnit    = item.Candidate?.IsBlockUnit == true,
                ProjectUnitId  = item.Candidate is { IsBlockUnit: true } u ? units.GetValueOrDefault(u.Id) : null,
                Requestor      = command.Requestor,
                Creator        = command.Creator,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "[REAPPRAISAL-INITIATE] Group {GroupNumber}: published {Count} event(s), skipped {SkipCount}",
            groupNumber, toProcess.Count, skipped.Count);

        // Return accepted count; CreatedRequestIds are populated asynchronously by the consumer.
        var acceptedAppraisalIds = toProcess
            .Where(i => i.AppraisalId.HasValue)
            .Select(i => i.AppraisalId!.Value)
            .ToList();

        return new InitiateReappraisalResult(
            groupNumber, acceptedAppraisalIds, skipped, toProcess.Count,
            toProcess.Select(i => new AcceptedReappraisalItem(i.AppraisalNumber!, i.AppraisalId)).ToList());
    }

    private async Task<HashSet<Guid>> FindDueCandidateIdsAsync(IReadOnlyList<Guid> ids, CancellationToken _)
    {
        // Through the view: it is the one definition of "on the latest file". Only Id/IsInLatestFile are
        // read, so its per-row APPLYs are unreferenced and eligible for the optimizer to drop.
        // ponytail: not measured; move the latest-file rule into its own view if this shows up slow.
        const string sql = """
            SELECT c.Id
            FROM collateral.vw_ReappraisalCandidates c
            WHERE c.Id IN @Ids
              AND c.IsInLatestFile = 1
            """;
        return (await QueryChunkedAsync<Guid, Guid>(sql, ids, chunk => new { Ids = chunk })).ToHashSet();
    }

    // SQL Server caps a statement at 2,100 parameters; every IN list here is sent in chunks.
    private const int ChunkSize = 1000;

    private async Task<List<T>> QueryChunkedAsync<T, TKey>(
        string sql, IReadOnlyList<TKey> keys, Func<TKey[], object> parameters)
    {
        var rows = new List<T>();
        var connection = connectionFactory.GetOpenConnection();
        foreach (var chunk in keys.Chunk(ChunkSize))
            rows.AddRange(await connection.QueryAsync<T>(sql, parameters(chunk)));
        return rows;
    }

    private static string BookNumber(ReappraisalCandidate c) =>
        c.NormalizedSurveyNumber ?? As400AppraisalNumber.Normalize(c.SurveyNumber);

    // ── Dapper helpers ─────────────────────────────────────────────────────────

    private async Task<Dictionary<string, Guid>> ResolvePrevAppraisalIdsAsync(
        IReadOnlyList<string> surveyNumbers,
        CancellationToken _)
    {
        const string sql = """
            -- MIN(Id) in SQL: one appraisal per number even if two live ones share it, picked by the
            -- same ordering as the detail page and the candidate view (SQL's, not .NET's Guid order).
            SELECT MIN(a.Id) AS Id, a.AppraisalNumber AS SurveyNumber
            FROM appraisal.Appraisals a
            WHERE a.AppraisalNumber IN @SurveyNumbers
              AND a.IsDeleted = 0
            GROUP BY a.AppraisalNumber
            """;

        var rows = await QueryChunkedAsync<PrevAppraisalRow, string>(
            sql, surveyNumbers, chunk => new { SurveyNumbers = chunk });

        // Case-insensitive like the SQL match and every other book map here; one id per number even if
        // two live appraisals share it (deterministic, and no duplicate-key failure).
        return rows
            .GroupBy(r => r.SurveyNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<NearbyAppraisalRow>> FetchAppraisalNumbersAsync(
        IReadOnlyList<Guid> appraisalIds,
        CancellationToken _)
    {
        const string sql = """
            SELECT a.Id AS AppraisalId, a.AppraisalNumber
            FROM appraisal.Appraisals a
            WHERE a.Id IN @AppraisalIds
            """;

        return await QueryChunkedAsync<NearbyAppraisalRow, Guid>(
            sql, appraisalIds, chunk => new { AppraisalIds = chunk });
    }

    /// <summary>
    /// Reappraisals of the books, as rows: open (an open reappraisal — appraisal.vw_ReappraisalsByBook,
    /// every way an appraisal reappraises a book — or a request still waiting to enter the workflow,
    /// request.vw_WaitingReappraisalRequests) or completed, with the collateral each was raised for when
    /// known (block-project units are matched on it). One round trip per chunk.
    /// </summary>
    private async Task<List<BookReviewRow>> FindBooksUnderReviewAsync(
        IReadOnlyList<string> bookNumbers,
        CancellationToken _)
    {
        const string sql = """
            SELECT rb.BookNumber, rb.CollateralId,
                   CAST(CASE WHEN rb.Status = 'Completed' THEN 0 ELSE 1 END AS bit) AS IsOpen
            FROM appraisal.vw_ReappraisalsByBook rb
            WHERE rb.BookNumber IN @BookNumbers
              AND rb.Status <> 'Cancelled'
            UNION
            SELECT w.BookNumber, w.CollateralId, CAST(1 AS bit)
            FROM request.vw_WaitingReappraisalRequests w
            WHERE w.BookNumber IN @BookNumbers
            """;

        return await QueryChunkedAsync<BookReviewRow, string>(
            sql, bookNumbers, chunk => new { BookNumbers = chunk });
    }

    /// <summary>The project unit each block-project candidate matched (collateral.vw_ReappraisalCandidateUnits).</summary>
    private async Task<Dictionary<Guid, Guid?>> FindUnitsAsync(IReadOnlyList<Guid> candidateIds, CancellationToken _)
    {
        const string sql = """
            SELECT u.CandidateId, u.ProjectUnitId
            FROM collateral.vw_ReappraisalCandidateUnits u
            WHERE u.CandidateId IN @Ids
            """;
        var rows = await QueryChunkedAsync<UnitRow, Guid>(sql, candidateIds, chunk => new { Ids = chunk });
        return rows.ToDictionary(r => r.CandidateId, r => r.ProjectUnitId);
    }

    /// <summary>
    /// A legacy book's prior value/date for the request: its latest valuation in the bank's listing
    /// (appraisal.vw_LegacyBookLatestValuation, shared with the list and the request page) — never the
    /// COLLATREV row's own copies. A book not in the listing gets none.
    /// </summary>
    private async Task<Dictionary<string, LegacyPriorRow>> FindLegacyPriorsAsync(IReadOnlyList<string> books, CancellationToken _)
    {
        // IN ignores the char-padded BookNumber's trailing spaces; RTRIM only for the returned key.
        const string sql = """
            SELECT RTRIM(lv.BookNumber) AS Book, lv.ValuationPriceInBaht AS Value, lv.ValuationDate AS Date
            FROM appraisal.vw_LegacyBookLatestValuation lv
            WHERE lv.BookNumber IN @Books
            """;
        var rows = await QueryChunkedAsync<LegacyPriorRow, string>(sql, books, chunk => new { Books = chunk });
        return rows.ToDictionary(r => r.Book, StringComparer.OrdinalIgnoreCase);
    }

    // ── Private record types ───────────────────────────────────────────────────

    private sealed record PrevAppraisalRow(Guid Id, string SurveyNumber);
    private sealed record NearbyAppraisalRow(Guid AppraisalId, string? AppraisalNumber);
    private sealed record BookReviewRow(string BookNumber, string? CollateralId, bool IsOpen);
    private sealed record UnitRow(Guid CandidateId, Guid? ProjectUnitId);
    private sealed record LegacyPriorRow(string Book, decimal? Value, DateTime? Date);

    private sealed record WorkingItem(
        Guid? AppraisalId,
        string? AppraisalNumber,
        ReappraisalCandidate? Candidate,
        bool FromNearby);
}
