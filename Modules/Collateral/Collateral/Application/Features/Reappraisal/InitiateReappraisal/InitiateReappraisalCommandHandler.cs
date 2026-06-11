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
///   2. Resolve SurveyNumber → AppraisalId via Dapper cross-schema query.
///   3. Layer 1 dedupe — remove any AppraisalId already in-flight.
///   4. Generate one shared group number.
///   5. MarkConsumed each matched candidate.
///   6. Publish one <see cref="ReappraisalInitiatedIntegrationEvent"/> per working item
///      via the Collateral outbox.
///   7. SaveChanges (candidates + outbox messages in one transaction).
///
/// Request-side work (async, handled by <c>ReappraisalInitiatedIntegrationEventHandler</c>):
///   Creates and submits one reappraisal Request per event message.
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

        // ── Step 1: Load Pending candidates (CandidateIds path) ──────────────
        var candidates = hasCandidates
            ? await dbContext.ReappraisalCandidates
                .Where(c => command.CandidateIds.Contains(c.Id)
                            && c.Status == ReappraisalCandidateStatus.Pending)
                .ToListAsync(cancellationToken)
            : new List<ReappraisalCandidate>();

        if (hasCandidates && candidates.Count == 0)
            throw new InvalidOperationException(
                "No Pending candidates found for the provided CandidateIds.");

        if (hasCandidates && candidates.Count != command.CandidateIds.Count)
        {
            var found   = candidates.Select(c => c.Id).ToHashSet();
            var missing = command.CandidateIds.Where(id => !found.Contains(id)).ToList();
            logger.LogWarning(
                "[REAPPRAISAL-INITIATE] {Count} candidates not found or not Pending: {Ids}",
                missing.Count, string.Join(", ", missing));
        }

        // ── Step 2: Resolve SurveyNumber → PrevAppraisalId for all candidates ─
        var surveyNumbers       = candidates.Select(c => c.SurveyNumber).Distinct().ToList();
        var appraisalIdBySurvey = await ResolvePrevAppraisalIdsAsync(surveyNumbers, cancellationToken);

        // ── Step 3: Build working list ─────────────────────────────────────────
        var workingItems = candidates
            .Select(c =>
            {
                appraisalIdBySurvey.TryGetValue(c.SurveyNumber, out var appraisalId);
                return new WorkingItem(appraisalId, c.SurveyNumber, c);
            })
            .ToList();

        // NearbyAppraisalIds path: fetch appraisal numbers, find any matching Pending candidate
        if (hasNearby)
        {
            var nearbyRows = await FetchAppraisalNumbersAsync(command.NearbyAppraisalIds, cancellationToken);

            var candidateBySurvey = candidates
                .GroupBy(c => c.SurveyNumber, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var row in nearbyRows)
            {
                if (workingItems.Any(w => w.AppraisalId == row.AppraisalId))
                    continue;

                ReappraisalCandidate? matchedCandidate = null;
                if (!string.IsNullOrWhiteSpace(row.AppraisalNumber))
                {
                    if (!candidateBySurvey.TryGetValue(row.AppraisalNumber, out matchedCandidate))
                    {
                        matchedCandidate = await dbContext.ReappraisalCandidates
                            .FirstOrDefaultAsync(c => c.SurveyNumber == row.AppraisalNumber
                                                      && c.Status == ReappraisalCandidateStatus.Pending,
                                cancellationToken);
                    }
                }

                workingItems.Add(new WorkingItem(row.AppraisalId, row.AppraisalNumber, matchedCandidate));
            }
        }

        // ── Step 4: Layer 1 dedupe ─────────────────────────────────────────────
        var allAppraisalIds = workingItems
            .Where(w => w.AppraisalId.HasValue)
            .Select(w => w.AppraisalId!.Value)
            .Distinct()
            .ToList();

        var inFlightIds = await FindInFlightAppraisalIdsAsync(allAppraisalIds, cancellationToken);

        var skipped   = new List<SkippedReappraisalItem>();
        var toProcess = new List<WorkingItem>();

        foreach (var item in workingItems)
        {
            if (item.AppraisalId.HasValue && inFlightIds.Contains(item.AppraisalId.Value))
            {
                skipped.Add(new SkippedReappraisalItem(
                    item.AppraisalId.Value,
                    item.AppraisalNumber,
                    "AlreadyInFlight"));

                logger.LogInformation(
                    "[REAPPRAISAL-INITIATE] Skipped AppraisalId {AppraisalId} ({Number}) — already in-flight",
                    item.AppraisalId, item.AppraisalNumber);
            }
            else
            {
                toProcess.Add(item);
            }
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

        // ── Step 6: MarkConsumed + publish outbox events ──────────────────────
        foreach (var item in toProcess)
        {
            item.Candidate?.MarkConsumed();

            outbox.Publish(new ReappraisalInitiatedIntegrationEvent
            {
                GroupNumber    = groupNumber,
                Source         = item.Candidate is not null ? "Candidate" : "InSystem",
                CandidateId    = item.Candidate?.Id,
                SurveyNumber   = item.Candidate?.SurveyNumber ?? item.AppraisalNumber,
                CifNumber      = item.Candidate?.CifNumber,
                CifName        = item.Candidate?.CifName,
                CollateralId   = item.Candidate?.CollateralId,
                PrevAppraisalId = item.AppraisalId,
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

        return new InitiateReappraisalResult(groupNumber, acceptedAppraisalIds, skipped);
    }

    // ── Dapper helpers ─────────────────────────────────────────────────────────

    private async Task<Dictionary<string, Guid>> ResolvePrevAppraisalIdsAsync(
        IReadOnlyList<string> surveyNumbers,
        CancellationToken _)
    {
        if (surveyNumbers.Count == 0) return new Dictionary<string, Guid>();

        const string sql = """
            SELECT a.Id AS Id, a.AppraisalNumber AS SurveyNumber
            FROM appraisal.Appraisals a
            WHERE a.AppraisalNumber IN @SurveyNumbers
            """;

        var rows = await connectionFactory.GetOpenConnection()
            .QueryAsync<PrevAppraisalRow>(sql, new { SurveyNumbers = surveyNumbers });

        return rows.ToDictionary(r => r.SurveyNumber, r => r.Id);
    }

    private async Task<IReadOnlyList<NearbyAppraisalRow>> FetchAppraisalNumbersAsync(
        IReadOnlyList<Guid> appraisalIds,
        CancellationToken _)
    {
        if (appraisalIds.Count == 0) return [];

        const string sql = """
            SELECT a.Id AS AppraisalId, a.AppraisalNumber
            FROM appraisal.Appraisals a
            WHERE a.Id IN @AppraisalIds
            """;

        var rows = await connectionFactory.GetOpenConnection()
            .QueryAsync<NearbyAppraisalRow>(sql, new { AppraisalIds = appraisalIds });

        return rows.ToList();
    }

    private async Task<HashSet<Guid>> FindInFlightAppraisalIdsAsync(
        IReadOnlyList<Guid> appraisalIds,
        CancellationToken _)
    {
        if (appraisalIds.Count == 0) return [];

        const string sql = """
            SELECT DISTINCT a.PrevAppraisalId
            FROM appraisal.Appraisals a
            WHERE a.Status NOT IN ('Completed', 'Cancelled')
              AND a.IsDeleted = 0
              AND a.PrevAppraisalId IN @AppraisalIds
            """;

        var rows = await connectionFactory.GetOpenConnection()
            .QueryAsync<Guid>(sql, new { AppraisalIds = appraisalIds });

        return rows.ToHashSet();
    }

    // ── Private record types ───────────────────────────────────────────────────

    private sealed record PrevAppraisalRow(Guid Id, string SurveyNumber);
    private sealed record NearbyAppraisalRow(Guid AppraisalId, string? AppraisalNumber);

    private sealed record WorkingItem(
        Guid? AppraisalId,
        string? AppraisalNumber,
        ReappraisalCandidate? Candidate);
}
