using Appraisal.Application.Features.Appraisals.CreateLandProperty;

public record SaveLandPMAPropertyDraftRequest(
    decimal? SellingPrice,
    decimal? ForcedSalePrice,
    decimal? BuildingInsurancePrice,
    List<LandTitleItemData>? Titles = null,
    string? SubDistrict = null,
    string? District = null,
    string? Province = null
);
