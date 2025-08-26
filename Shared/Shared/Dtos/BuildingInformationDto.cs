namespace Shared.Dtos;

public record BuildingInformationDto(
    string NoHouseNumber,
    decimal? LandArea,
    string? BuildingCondition,
    string? BuildingStatus,
    DateTime? LicenseExpirationDate,
    string? IsAppraise,
    ObligationDetailDto ObligationDetail
);
