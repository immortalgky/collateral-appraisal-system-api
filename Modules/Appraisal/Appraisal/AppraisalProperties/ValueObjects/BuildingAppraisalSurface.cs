namespace Appraisal.AppraisalProperties.ValueObjects;

public class BuildingAppraisalSurface : ValueObject
{
    public short? FromFloorNo { get; }
    public short? ToFloorNo { get; }
    public string? FloorType { get; }
    public string? FloorStructure { get; }
    public string? FloorStructureOther { get; }
    public string? FloorSurface { get; }
    public string? FloorSurfaceOther { get; }

    private BuildingAppraisalSurface(
        short? fromFloorNo,
        short? toFloorNo,
        string? floorType,
        string? floorStructure,
        string? floorStructureOther,
        string? floorSurface,
        string? floorSurfaceOther
    )
    {
        FromFloorNo = fromFloorNo;
        ToFloorNo = toFloorNo;
        FloorType = floorType;
        FloorStructure = floorStructure;
        FloorStructureOther = floorStructureOther;
        FloorSurface = floorSurface;
        FloorSurfaceOther = floorSurfaceOther;
    }

    public static BuildingAppraisalSurface Create(
        short? fromFloorNo,
        short? toFloorNo,
        string? floorType,
        string? floorStructure,
        string? floorStructureOther,
        string? floorSurface,
        string? floorSurfaceOther
    )
    {
        return new BuildingAppraisalSurface(
            fromFloorNo,
            toFloorNo,
            floorType,
            floorStructure,
            floorStructureOther,
            floorSurface,
            floorSurfaceOther
        );
    }
}
