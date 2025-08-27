namespace Appraisal.Contracts.Appraisals.Dto;

public record BuildingTypeDetailDto(
    string BuildingType,
    string? BuildingTypeOther,
    short? TotalFloor
);
