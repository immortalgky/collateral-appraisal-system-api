namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDetailList;

public record GetSupportingDetailListResult(IEnumerable<SupportingDetailListItem> Items, int TotalCount, int PageNumber, int PageSize);

public record SupportingDetailListItem(
    Guid Id,
    string? PropertyName,
    string? Developer,
    string? ModelName,
    string CollateralType,
    string BuildingType,
    decimal? LandArea,
    decimal? UsableArea,
    string? ProjectName,
    string? RoomFloor,
    string? HouseNo,
    string? SubDistrict,
    string? District,
    string? Province,
    decimal? Latitude,
    decimal? Longitude,
    List<string?> PlotLocationType,
    string? PlotLocationTypeOther,
    decimal? PricePerUnit,
    decimal? OfferingPrice,
    decimal? SellingPrice,
    string? PhoneNo,
    DateTime InformationDate,
    string? Website,
    string? SourceUrl,
    string? Remark
);