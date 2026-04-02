namespace Appraisal.Application.Features.PricingAnalysis.SaveMachineCostItems;

public record SaveMachineCostItemsResponse(
    Guid PricingAnalysisId,
    Guid MethodId,
    int ItemCount,
    decimal TotalFmv
);
