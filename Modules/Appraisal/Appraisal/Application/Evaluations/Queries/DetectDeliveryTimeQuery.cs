using System.Text.Json;
using Shared.Sla;

namespace Appraisal.Application.Evaluations.Queries;

/// <summary>
/// Auto-detects delivery time for the current External assignment of the given appraisal.
/// Computes first-submission turnaround (SubmittedAt − AssignedAt) in business hours, converted
/// to 8-hour business days, and suggests a 1–5 rating.
///
/// Rating thresholds are loaded from appraisal.EvaluationCriteriaConfigs (slot 2 / Delivery)
/// for the appraisal's BankingSegment. ThresholdsJson stores {"5": upperBound, "4": ..., "3": ..., "2": ...}
/// where the rating is assigned to the highest level whose upper-bound-days >= measured days.
/// Falls back to hardcoded Retail defaults if config or thresholds are missing.
///
/// Returns null when no qualifying External assignment exists, or when either timestamp is null.
/// </summary>
public record DetectDeliveryTimeQuery(Guid AppraisalId)
    : IQuery<DetectDeliveryTimeResult?>;

public record DetectDeliveryTimeResult(decimal DetectedDays, int SuggestedRating);

public class DetectDeliveryTimeQueryHandler(
    AppraisalDbContext dbContext,
    IBusinessTimeCalculator businessTimeCalculator)
    : IQueryHandler<DetectDeliveryTimeQuery, DetectDeliveryTimeResult?>
{
    public async Task<DetectDeliveryTimeResult?> Handle(
        DetectDeliveryTimeQuery query,
        CancellationToken cancellationToken)
    {
        // Matches vw_AppraisalEvaluationList CTE: most recent External assignment,
        // excluding Rejected/Cancelled. The Rejected/Cancelled exclusion is also a
        // global query filter on AppraisalAssignment, but we restate it here so this
        // handler stays correct even if a caller adds IgnoreQueryFilters() upstream.
        // Order mirrors: ORDER BY AssignedAt DESC, CreatedAt DESC, Id DESC.
        var assignment = await dbContext.AppraisalAssignments
            .AsNoTracking()
            .Where(a => a.AppraisalId == query.AppraisalId
                        && a.AssignmentType == AssignmentType.External
                        && a.AssignmentStatus != AssignmentStatus.Rejected
                        && a.AssignmentStatus != AssignmentStatus.Cancelled)
            .OrderByDescending(a => a.AssignedAt)
            .ThenByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignment is null || assignment.AssignedAt is null || assignment.SubmittedAt is null)
            return null;

        var minutes = await businessTimeCalculator.GetBusinessMinutesBetweenAsync(
            assignment.AssignedAt.Value,
            assignment.SubmittedAt.Value,
            cancellationToken);

        // Defensive: SubmittedAt < AssignedAt (clock skew / out-of-order writes) → no suggestion.
        if (minutes < 0) return null;

        var days = Math.Round((decimal)minutes / 60m / 8m, 2);

        // ── Load BankingSegment for this appraisal ─────────────────────────────
        var bankingSegment = await dbContext.Appraisals
            .AsNoTracking()
            .Where(a => a.Id == query.AppraisalId)
            .Select(a => a.BankingSegment)
            .FirstOrDefaultAsync(cancellationToken);

        // Defaults to IBG when null (matches view ISNULL fallback)
        var effectiveSegment = bankingSegment ?? "IBG";

        // ── Load Delivery config row for that segment ─────────────────────────
        var deliveryConfig = await dbContext.EvaluationCriteriaConfigs
            .AsNoTracking()
            .Where(c => c.BankingSegment.ToLower() == effectiveSegment.ToLower()
                        && c.CriteriaSlot == 2)
            .FirstOrDefaultAsync(cancellationToken);

        // ── Parse thresholds and map to rating ────────────────────────────────
        if (deliveryConfig?.ThresholdsJson is not null)
        {
            var rating = MapDaysToRatingFromThresholds(days, deliveryConfig.ThresholdsJson);
            return new DetectDeliveryTimeResult(days, rating);
        }

        // ── Fallback: hardcoded Retail thresholds ─────────────────────────────
        // Use <= to match the config path's boundary semantics (e.g. "within 2 days" → 5).
        var fallbackRating = days switch
        {
            <= 2.0m => 5,
            <= 2.5m => 4,
            <= 3.0m => 3,
            <= 3.5m => 2,
            _       => 1
        };

        return new DetectDeliveryTimeResult(days, fallbackRating);
    }

    /// <summary>
    /// Parses ThresholdsJson {"5": upperBound, "4": upperBound, ...} and returns
    /// the highest rating level whose upper-bound-days >= measured days.
    /// Returns 1 when no threshold matches (days exceeds all upper bounds).
    /// </summary>
    private static int MapDaysToRatingFromThresholds(decimal days, string thresholdsJson)
    {
        try
        {
            // Thresholds are keyed by rating string ("5","4","3","2"); value = upper-bound days.
            var thresholds = JsonSerializer.Deserialize<Dictionary<string, decimal>>(thresholdsJson);
            if (thresholds is null) return 1;

            // Evaluate from highest rating down; first match wins.
            foreach (var rating in new[] { 5, 4, 3, 2 })
            {
                if (thresholds.TryGetValue(rating.ToString(), out var upperBound) && days <= upperBound)
                    return rating;
            }

            // No threshold matched → worst rating
            return 1;
        }
        catch
        {
            // Malformed JSON — fall back to worst rating rather than crashing.
            return 1;
        }
    }
}
