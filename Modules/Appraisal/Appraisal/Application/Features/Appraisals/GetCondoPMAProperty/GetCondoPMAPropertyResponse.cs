public record GetCondoPMAPropertyResponse(
    // Property
    Guid PropertyId,
    Guid AppraisalId,
    decimal? BuildingInsurancePrice,
    decimal? SellingPrice,
    decimal? ForceSellingPrice,
    string? CondoName,
    string? BuildingNumber,
    string? BuiltOnTitleNumber,
    string? CondoRegistrationNumber,
    string? RoomNumber,
    string? FloorNumber,
    string? SubDistrict,
    string? District,
    string? Province
);