namespace Appraisal.Contracts.Appraisals.Dto;

public record BuildingInformationDto(
    string HouseNumberStatus,
    decimal? LandArea,
    string? BuildingCondition,
    string? BuildingStatus,
    DateTime? LicenseExpirationDate,
    string? IsAppraise,
    ObligationDetailDto ObligationDetail
);
