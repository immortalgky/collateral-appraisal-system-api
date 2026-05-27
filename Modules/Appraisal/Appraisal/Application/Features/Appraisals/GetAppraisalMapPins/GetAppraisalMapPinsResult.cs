namespace Appraisal.Application.Features.Appraisals.GetAppraisalMapPins;

public record GetAppraisalMapPinsResult(
    IReadOnlyList<AppraisalMapCollateralPinDto> Collateral,
    IReadOnlyList<AppraisalMapComparablePinDto> MarketComparables);

public record AppraisalMapCollateralPinDto(
    Guid AppraisalPropertyId,
    decimal Lat,
    decimal Lon,
    string? PropertyType,
    string? Province,
    string? District,
    string? SubDistrict);

public record AppraisalMapComparablePinDto(
    Guid MarketComparableId,
    decimal Lat,
    decimal Lon,
    string PropertyType,
    string SurveyName,
    DateTime? InfoDateTime,
    decimal? OfferPrice,
    decimal? SalePrice);
