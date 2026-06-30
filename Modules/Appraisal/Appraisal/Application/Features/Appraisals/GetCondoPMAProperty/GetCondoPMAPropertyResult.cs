public record GetCondoPMAPropertyResult(
    // Property
    Guid PropertyId,
    Guid AppraisalId,
    decimal? BuildingInsurancePrice,
    decimal? SellingPrice,
    decimal? ForceSellingPrice,
    string? BuildingNumber,
    string? BuiltOnTitleNumber,
    string? CondoRegistrationNumber,
    string? RoomNumber,
    string? FloorNumber,
    string? SubDistrict,
    string? District,
    string? Province
);