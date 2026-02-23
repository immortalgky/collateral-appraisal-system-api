namespace Appraisal.Application.Features.PricingAnalysis.AddFactorScore;

/// <summary>
/// Result of adding a factor score
/// </summary>
public record AddFactorScoreResult(
    Guid FactorScoreId,
    Guid FactorId,
    decimal FactorWeight,
    string? SubjectValue,
    decimal? SubjectScore,
    string? ComparableValue,
    decimal? ComparableScore,
    decimal? ScoreDifference,
    decimal? WeightedScore,
    decimal? AdjustmentPct,
    int DisplaySequence
);
