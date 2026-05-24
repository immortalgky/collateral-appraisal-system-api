using Shared.Sla;

namespace Appraisal.Application.Evaluations.Queries;

/// <summary>
/// Auto-detects delivery time for the current External assignment of the given appraisal.
/// Computes first-submission turnaround (SubmittedAt − AssignedAt) in business hours, converted
/// to 8-hour business days, and suggests a 1–5 rating.
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

        var rating = days switch
        {
            < 2.0m => 5,
            < 2.5m => 4,
            < 3.0m => 3,
            < 3.5m => 2,
            _      => 1
        };

        return new DetectDeliveryTimeResult(days, rating);
    }
}
