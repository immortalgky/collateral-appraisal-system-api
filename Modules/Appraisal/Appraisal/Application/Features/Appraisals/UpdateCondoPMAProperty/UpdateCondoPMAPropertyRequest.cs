public record UpdateCondoPMAPropertyRequest(
    decimal? SellingPrice,
    decimal? ForcedSalePrice,
    decimal? BuildingInsurancePrice,
    string? CondoName = null,
    string? BuiltOnTitleNumber = null,
    string? CondoRegistrationNumber = null,
    string? RoomNumber = null,
    string? FloorNumber = null,
    string? BuildingNumber = null,
    string? SubDistrict = null,
    string? District = null,
    string? Province = null
);