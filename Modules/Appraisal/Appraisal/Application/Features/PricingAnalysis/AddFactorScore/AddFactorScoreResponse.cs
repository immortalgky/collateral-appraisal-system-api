namespace Appraisal.Application.Features.PricingAnalysis.AddFactorScore;

public record AddFactorScoreResponse(
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
