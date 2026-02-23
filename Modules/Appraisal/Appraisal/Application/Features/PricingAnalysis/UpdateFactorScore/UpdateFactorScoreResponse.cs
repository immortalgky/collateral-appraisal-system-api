namespace Appraisal.Application.Features.PricingAnalysis.UpdateFactorScore;

public record UpdateFactorScoreResponse(
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
