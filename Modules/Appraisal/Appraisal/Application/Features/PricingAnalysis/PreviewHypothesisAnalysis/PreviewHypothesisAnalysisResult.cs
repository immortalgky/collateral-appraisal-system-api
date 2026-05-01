using Appraisal.Domain.Appraisals.Hypothesis;
using Appraisal.Domain.Appraisals.Hypothesis.Summaries;
using Appraisal.Domain.Services;

namespace Appraisal.Application.Features.PricingAnalysis.PreviewHypothesisAnalysis;

public record PreviewHypothesisAnalysisResult(
    HypothesisVariant Variant,
    LandBuildingSummary? LandBuildingSummary,
    Dictionary<string, HypothesisCalculationService.LandBuildingModelAggregate>? Models,
    CondominiumSummary? CondominiumSummary
);
