namespace Appraisal.Application.Features.PricingAnalysis.SaveMachineCostItems;

public record SaveMachineCostItemsResult(
    Guid PricingAnalysisId,
    Guid MethodId,
    int ItemCount,
    decimal TotalFmv
);
