namespace Appraisal.Application.Features.PricingAnalysis.UpdateFactorScore;

/// <summary>
/// Result of updating a factor score
/// </summary>
public record UpdateFactorScoreResult(
    Guid FactorScoreId,
    Guid FactorId,
    decimal FactorWeight,
    string? SubjectValue,
    decimal? SubjectScore,
    string? ComparableValue,
    decimal? ComparableScore,
    decimal? ScoreDifference,
    decimal? WeightedScore,
    decimal? AdjustmentPct
);
