namespace Appraisal.Contracts.Appraisals.Dto;

public record BuildingAppraisalSurfaceDto(
    short? FromFloorNumber,
    short? ToFloorNumber,
    string? FloorType,
    string? FloorStructureType,
    string? FloorStructureTypeOther,
    string? FloorSurfaceType,
    string? FloorSurfaceTypeOther
);
