namespace Appraisal.Contracts.Appraisals.Dto;

public record BuildingAppraisalSurfaceDto(
    Guid Id,
    int FromFloorNumber,
    int ToFloorNumber,
    string? FloorType,
    string? FloorStructureType,
    string? FloorStructureTypeOther,
    string? FloorSurfaceType,
    string? FloorSurfaceTypeOther
);
