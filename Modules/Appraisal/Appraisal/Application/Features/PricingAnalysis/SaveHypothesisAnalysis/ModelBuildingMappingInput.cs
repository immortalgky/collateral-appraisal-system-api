namespace Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis;

/// <summary>
/// L&amp;B: which building property a house model is priced like, plus the appraiser's optional
/// typed-over model total (C21). Also the GET shape. See HypothesisModelBuildingMapping.
/// </summary>
public record ModelBuildingMappingInput(
    string ModelName,
    Guid? AppraisalPropertyId,
    decimal? TotalCost
);
