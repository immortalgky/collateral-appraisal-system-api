using Appraisal.Domain.Appraisals.Hypothesis;
using Appraisal.Domain.Appraisals.Hypothesis.Summaries;

namespace Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis;

public record SaveHypothesisAnalysisResult(
    Guid HypothesisAnalysisId,
    HypothesisVariant Variant,
    LandBuildingSummary? LandBuildingSummary,
    CondominiumSummary? CondominiumSummary
);
