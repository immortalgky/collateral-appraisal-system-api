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
    // Skip reasons (see SkippedReappraisalItem).
    private const string ReasonAlreadyInFlight = "AlreadyInFlight";
    private const string ReasonAlreadyReviewed = "AlreadyReviewed";
    private const string ReasonNoBookNumber = "NoBookNumber";
    private const string ReasonNotDue = "NotDue";
    private const string ReasonPriorNotCompleted = "PriorNotCompleted";

    // Precedence of a skip reason when one book collects several (a higher rank replaces a lower one).
    private static int ReasonRank(string reason) => reason switch
    {
        ReasonNoBookNumber => 4,
        ReasonAlreadyInFlight or ReasonAlreadyReviewed => 3,
        ReasonPriorNotCompleted => 2,
        _ => 1,   // NotDue
    };

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
                // One book can be selected under several collaterals; a fixed order (CollateralId as text,
                // then Id — not numeric) makes the pick below, and the order requests are created in, the
                // same on every run.
                .OrderBy(c => c.CollateralId).ThenBy(c => c.Id)
                .ToListAsync(cancellationToken)
            : new List<ReappraisalCandidate>();
        if (pending.Count == 0 && !hasNearby)
            throw new InvalidOperationException("No Pending candidates found for the provided CandidateIds.");

        // ── Step 2: Resolve the book number → PrevAppraisalId (for the skips too) ─
        var surveyNumbers       = pending.Select(BookNumber).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var (appraisalIdBySurvey, unusableBySurvey) = await ResolvePrevAppraisalIdsAsync(surveyNumbers, cancellationToken);
        // The completed prior's id, or null (no completed appraisal: legacy, or one that cannot be a prior).
        Guid? PrevId(string book) => appraisalIdBySurvey.TryGetValue(book, out var id) ? id : null;
        // What a skip reports: that id, else the live appraisal that cannot be one, else null (not in CAS).
        Guid? ReportedId(string book) =>
            PrevId(book) ?? (unusableBySurvey.TryGetValue(book, out var liveId) ? liveId : null);

        var dueIds = await FindDueCandidateIdsAsync(pending.Select(c => c.Id).ToList(), cancellationToken);

        if (hasCandidates)
        {
            var found   = pending.Where(c => dueIds.Contains(c.Id)).Select(c => c.Id).ToHashSet();
            var missing = command.CandidateIds.Where(id => !found.Contains(id)).ToList();
            if (missing.Count > 0)
                logger.LogWarning(
                    "[REAPPRAISAL-INITIATE] {Count} candidates not found, not Pending, or not on the latest file: {Ids}",
                    missing.Count, string.Join(", ", missing));
        }

        // The nearby appraisals are read up front: their books count in the under-review check below.
        var nearbyRows = hasNearby
            ? (await FetchAppraisalNumbersAsync(command.NearbyAppraisalIds, cancellationToken))
                .DistinctBy(r => r.AppraisalId).ToList()
            : [];
        if (hasNearby)
        {
            var foundNearby = nearbyRows.Select(r => r.AppraisalId).ToHashSet();
            var missingNearby = command.NearbyAppraisalIds.Where(id => !foundNearby.Contains(id)).Distinct().ToList();
            if (missingNearby.Count > 0)
                logger.LogWarning(
                    "[REAPPRAISAL-INITIATE] {Count} nearby appraisals not found: {Ids}",
                    missingNearby.Count, string.Join(", ", missingNearby));
        }

        // Under review = an open reappraisal of the book, or a request for it still waiting. A book is reviewed once,
        // whichever collateral it is listed under: the request copies the prior request's titles, so it covers every
        // collateral of the book — the same grain as consumption on submit. A nearby book is also refused once any
        // reappraisal of it has completed — the nearby grid hides those (a completed reappraisal supersedes the
        // book), so only a stale tab or a direct call can send one, and initiating it would fork the chain from the
        // superseded book. A block-project unit is the exception to "once per book": each collateral is a different
        // unit, so its checks and the in-batch dedupe are per collateral, against reappraisals raised for it.
        var reviewRows = await FindBooksUnderReviewAsync(
            surveyNumbers.Concat(nearbyRows.Select(r => r.AppraisalNumber).OfType<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            cancellationToken);
        bool Matches(BookReviewRow r, string book, ReappraisalCandidate? candidate) =>
            string.Equals(r.BookNumber, book, StringComparison.OrdinalIgnoreCase)
            && (candidate is not { IsBlockUnit: true } || r.CollateralId == candidate.CollateralId);
        bool UnderReview(string book, ReappraisalCandidate? candidate) =>
            reviewRows.Any(r => r.IsOpen && Matches(r, book, candidate));

        // One skip row per BOOK across all reasons (however many candidates, collaterals or block units were selected
        // for it); every skip is logged. reportedBooks maps each reported book to its reason and its row in
        // `skipped`; when a later candidate of the same book has a HIGHER-precedence reason (AlreadyInFlight >
        // PriorNotCompleted > NotDue) the row is upgraded, so the outcome does not depend on the order. A row without
        // a book number is keyed by `key` (its candidate or appraisal id).
        var reportedBooks = new Dictionary<string, (string Reason, int Row)>(StringComparer.OrdinalIgnoreCase);
        void Skip(Guid? appraisalId, string? number, string reason, string? key = null)
        {
            var bookKey = key ?? number ?? appraisalId?.ToString() ?? string.Empty;
            var item = new SkippedReappraisalItem(appraisalId, number, reason);
            if (!reportedBooks.TryGetValue(bookKey, out var reported))
            {
                reportedBooks[bookKey] = (reason, skipped.Count);
                skipped.Add(item);
            }
            else if (ReasonRank(reason) > ReasonRank(reported.Reason))
            {
                skipped[reported.Row] = item;
                reportedBooks[bookKey] = (reason, reported.Row);
            }
            logger.LogInformation(
                "[REAPPRAISAL-INITIATE] Skipped {Number} (AppraisalId {AppraisalId}) — {Reason}",
                number, appraisalId, reason);
        }

        // Precedence for a candidate: AlreadyInFlight, then PriorNotCompleted (a book with a live appraisal that is
        // not completed is not unknown to CAS, and a legacy request would carry its number as a prior with nothing
        // behind it), then NotDue (not on AS400's latest file: an old copy, or no longer due). A book with a
        // candidate that is accepted shows no NotDue row for its other copies: it was initiated.
        var classified = pending.Select(c =>
        {
            var book = BookNumber(c);
            // Every request must carry its book (the key for waiting / in-progress checks and for consumption on
            // submit); the consumer refuses a message without one.
            var (reason, id) =
                string.IsNullOrWhiteSpace(book) ? (ReasonNoBookNumber, (Guid?)null)
                : UnderReview(book, c) ? (ReasonAlreadyInFlight, ReportedId(book))
                : unusableBySurvey.TryGetValue(book, out var liveId) ? (ReasonPriorNotCompleted, liveId)
                : !dueIds.Contains(c.Id) ? (ReasonNotDue, ReportedId(book))
                : (null, null);
            return (Candidate: c, Book: book, Reason: reason, Id: id);
        }).ToList();
        var initiatedBooks = classified.Where(x => x.Reason is null).Select(x => x.Book)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = new List<ReappraisalCandidate>();
        foreach (var (c, book, reason, id) in classified)
        {
            if (reason is null)
                candidates.Add(c);
            else if (reason == ReasonNotDue && initiatedBooks.Contains(book))
                logger.LogInformation(
                    "[REAPPRAISAL-INITIATE] {Number}: collateral {CollateralId} is not due, but the book is initiated through another copy",
                    book, c.CollateralId);
            else if (reason == ReasonNoBookNumber)
                Skip(null, null, reason, key: c.Id.ToString());
            else
                Skip(id, book, reason);
        }

        // ── Step 3: Build working list ─────────────────────────────────────────
        var workingItems = candidates
            .Select(c => new WorkingItem(PrevId(BookNumber(c)), BookNumber(c), c, FromNearby: false))
            .ToList();

        // NearbyAppraisalIds path: find any matching Pending candidate for each picked appraisal
        if (hasNearby)
        {
            // A book has exactly one outcome per batch. acceptedBooks and the reported books (but not a NotDue one,
            // which is a different outcome) are decided: a nearby row for one is ignored, neither accepted nor
            // reported again.
            var acceptedBooks = new HashSet<string>(
                workingItems.Select(w => w.AppraisalNumber).OfType<string>(), StringComparer.OrdinalIgnoreCase);
            bool Decided(string book) =>
                acceptedBooks.Contains(book)
                || (reportedBooks.TryGetValue(book, out var reported) && reported.Reason != ReasonNotDue);

            // Pending candidates for the nearby books that are not decided yet: only copies on AS400's latest file
            // (the same rule as the CandidateIds path), the lowest CollateralId as text per book.
            var candidateBySurvey = new Dictionary<string, ReappraisalCandidate>(StringComparer.OrdinalIgnoreCase);
            var otherNumbers = nearbyRows
                .Where(r => r.IsUsablePrior)
                .Select(r => r.AppraisalNumber)
                .OfType<string>()
                .Where(n => !Decided(n))
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
                    // Ordinal: the same order as the SQL side (ORDER BY CollateralId), because collateral ids
                    // are digits only, where the collation and ordinal agree.
                    candidateBySurvey[g.Key] = g
                        .OrderBy(c => c.CollateralId, StringComparer.Ordinal).ThenBy(c => c.Id)
                        .First();
            }

            foreach (var row in nearbyRows)
            {
                // Every request must carry its book (the key for waiting / in-progress checks and for consumption on
                // submit); the consumer refuses a message without one, whatever the appraisal's status.
                if (string.IsNullOrWhiteSpace(row.AppraisalNumber))
                {
                    Skip(row.AppraisalId, null, ReasonNoBookNumber);
                    continue;
                }

                if (Decided(row.AppraisalNumber))
                    continue;

                // Same rule as the candidate path: an existing review wins, then only a completed prior can be bound.
                if (!row.IsUsablePrior)
                {
                    Skip(row.AppraisalId, row.AppraisalNumber,
                        UnderReview(row.AppraisalNumber, null) ? ReasonAlreadyInFlight : ReasonPriorNotCompleted);
                    continue;
                }

                candidateBySurvey.TryGetValue(row.AppraisalNumber, out var matchedCandidate);
                acceptedBooks.Add(row.AppraisalNumber);
                workingItems.Add(new WorkingItem(row.AppraisalId, row.AppraisalNumber, matchedCandidate, FromNearby: true));
            }
        }

        // ── Step 4: Layer 1 dedupe ─────────────────────────────────────────────
        // The candidates were checked against existing reviews above; a nearby row is checked here (under review, or
        // its reappraisal already completed). Then the same book selected twice in this batch (listed under two
        // collaterals) is ONE request, silently — unless it is a block-project unit, where each collateral is its own.
        var batchBooks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var toProcess = new List<WorkingItem>();

        foreach (var item in workingItems)
        {
            var book = item.AppraisalNumber!;
            if (item.FromNearby)
            {
                var reviewReason = UnderReview(book, item.Candidate) ? ReasonAlreadyInFlight
                    : reviewRows.Any(r => !r.IsOpen && Matches(r, book, item.Candidate)) ? ReasonAlreadyReviewed
                    : null;
                if (reviewReason is not null)
                {
                    Skip(item.AppraisalId, book, reviewReason);
                    continue;
                }
            }

            var batchKey = item.Candidate is { IsBlockUnit: true } c
                ? $"{book}|{c.CollateralId}"
                : book;
            if (batchBooks.Add(batchKey))
                toProcess.Add(item);
            else
                logger.LogInformation(
                    "[REAPPRAISAL-INITIATE] {Number}: already accepted once in this batch; collateral {CollateralId} not initiated again",
                    book, item.Candidate?.CollateralId);
        }

        // One outcome per book: a NotDue copy skipped earlier is dropped when the book is accepted (here, through a
        // nearby pick; a NotDue copy next to an accepted candidate was never reported).
        skipped.RemoveAll(s => s.Reason == ReasonNotDue
            && toProcess.Any(i => string.Equals(i.AppraisalNumber, s.OldAppraisalReportNumber, StringComparison.OrdinalIgnoreCase)));

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
                AutoUpdate     = item.Candidate?.AutoUpdate,
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

    /// <summary>
    /// The prior appraisal per book number: only a COMPLETED, undeleted appraisal can be bound as the prior
    /// (the request's purpose and prior are locked at Initiate). <c>Unusable</c> maps a number whose appraisal is live
    /// but not completed (in progress...) to that appraisal's id, so the caller can tell "in CAS but not usable" from
    /// "not in CAS" (legacy); a deleted or cancelled appraisal counts as not in CAS.
    /// </summary>
    private async Task<(Dictionary<string, Guid> Completed, Dictionary<string, Guid> Unusable)> ResolvePrevAppraisalIdsAsync(
        IReadOnlyList<string> surveyNumbers,
        CancellationToken _)
    {
        // IX_Appraisals_AppraisalNumber is unique on every non-null number (deleted rows included), so a number is
        // exactly one appraisal; the grouping below only guards against rows that differ by case alone.
        const string sql = """
            SELECT a.Id AS AppraisalId, a.AppraisalNumber, a.Status, a.IsDeleted
            FROM appraisal.Appraisals a
            WHERE a.AppraisalNumber IN @SurveyNumbers
            """;

        var rows = await QueryChunkedAsync<NearbyAppraisalRow, string>(
            sql, surveyNumbers, chunk => new { SurveyNumbers = chunk });

        // Case-insensitive like the SQL match and every other book map here; grouped so that two rows whose numbers
        // differ only by case (a collation quirk) cannot turn into a duplicate-key failure. A soft-deleted or
        // cancelled appraisal is in neither map: it does not count as "in CAS", so a book with only that takes the
        // legacy path again.
        Dictionary<string, Guid> ByNumber(IEnumerable<NearbyAppraisalRow> source) => source
            .GroupBy(r => r.AppraisalNumber!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.AppraisalId).First().AppraisalId,
                StringComparer.OrdinalIgnoreCase);
        var completed = ByNumber(rows.Where(r => r.IsUsablePrior));
        var unusable = ByNumber(rows.Where(r => r.IsLiveNotCompleted));
        return (completed, unusable);
    }

    private async Task<IReadOnlyList<NearbyAppraisalRow>> FetchAppraisalNumbersAsync(
        IReadOnlyList<Guid> appraisalIds,
        CancellationToken _)
    {
        const string sql = """
            SELECT a.Id AS AppraisalId, a.AppraisalNumber, a.Status, a.IsDeleted
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

    private sealed record NearbyAppraisalRow(Guid AppraisalId, string? AppraisalNumber, string Status, bool IsDeleted)
    {
        /// <summary>Only a completed, undeleted appraisal can be a request's prior.</summary>
        public bool IsUsablePrior => Status == "Completed" && !IsDeleted;

        /// <summary>
        /// Undeleted and not cancelled, but not completed (in progress...): known to CAS and not usable. A cancelled
        /// appraisal counts like a deleted one — not known to CAS.
        /// </summary>
        public bool IsLiveNotCompleted => !IsDeleted && Status is not ("Completed" or "Cancelled");
    }
    private sealed record BookReviewRow(string BookNumber, string? CollateralId, bool IsOpen);
    private sealed record UnitRow(Guid CandidateId, Guid? ProjectUnitId);
    private sealed record LegacyPriorRow(string Book, decimal? Value, DateTime? Date);

    private sealed record WorkingItem(
        Guid? AppraisalId,
        string? AppraisalNumber,
        ReappraisalCandidate? Candidate,
        bool FromNearby);
}
