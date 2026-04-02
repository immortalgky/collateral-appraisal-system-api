namespace Appraisal.Application.Features.PricingAnalysis.GetMachineCostItems;

public record GetMachineCostItemsResponse(
    List<MachineCostItemDto> Items,
    decimal TotalFmv,
    string? Remark
);
