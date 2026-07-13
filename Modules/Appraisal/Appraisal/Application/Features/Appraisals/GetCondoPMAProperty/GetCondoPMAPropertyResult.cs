public record GetCondoPMAPropertyResult(
    // Property
    Guid PropertyId,
    Guid AppraisalId,
    decimal? BuildingInsurancePrice,
    decimal? SellingPrice,
    decimal? ForcedSalePrice,
    string ExternalSyncStatus,
    string? ExternalSyncError,
    DateTime? ExternalSyncedAt,
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