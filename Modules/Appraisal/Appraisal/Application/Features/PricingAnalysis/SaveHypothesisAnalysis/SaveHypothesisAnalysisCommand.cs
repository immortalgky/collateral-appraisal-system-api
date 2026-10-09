using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis;

public record SaveHypothesisAnalysisCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    LandBuildingSummaryInput? LandBuildingSummary,
    CondominiumSummaryInput? CondominiumSummary,
    IReadOnlyList<HypothesisCostItemInput> CostItems,
    string? Remark,
    decimal? FinalValueOverride = null,
    decimal? IndicatedValue = null,
    IReadOnlyList<ModelBuildingMappingInput>? ModelBuildingMappings = null
) : ICommand<SaveHypothesisAnalysisResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
