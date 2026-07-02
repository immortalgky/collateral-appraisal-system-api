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
    decimal? FinalValueAdjusted = null,
    decimal? AppraisalPrice = null
) : ICommand<SaveHypothesisAnalysisResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
