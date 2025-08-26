namespace Shared.Dtos;

public record BuildingAppraisalSurfaceDto(
    short? FromFloorNo,
    short? ToFloorNo,
    string? FloorType,
    string? FloorStructure,
    string? FloorStructureOther,
    string? FloorSurface,
    string? FloorSurfaceOther
);
