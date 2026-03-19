namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Floor-by-floor surface details for building appraisals.
/// Owned by BuildingAppraisalDetail.
/// </summary>
public class BuildingAppraisalSurface : Entity<Guid>
{
    public Guid BuildingAppraisalDetailId { get; private set; }

    // Floor Range
    public int FromFloorNumber { get; private set; }
    public int ToFloorNumber { get; private set; }

    // Floor Details
    public string? FloorType { get; private set; } // Normal, Mezzanine, HighFoundation, Rooftop
    public string? FloorStructureType { get; private set; } // KSL, Wood, SmartBoard, Others
    public string? FloorStructureTypeOther { get; private set; }
    public string? FloorSurfaceType { get; private set; } // Granite, Wood, Tiles, Ceramic, Marble, Others
    public string? FloorSurfaceTypeOther { get; private set; }

    private BuildingAppraisalSurface()
    {
    }

    public static BuildingAppraisalSurface Create(
        Guid buildingAppraisalDetailId,
        int fromFloorNumber,
        int toFloorNumber,
        string? floorType = null,
        string? floorStructureType = null,
        string? floorStructureTypeOther = null,
        string? floorSurfaceType = null,
        string? floorSurfaceTypeOther = null)
    {
        if (toFloorNumber < fromFloorNumber)
            throw new ArgumentException("ToFloorNumber must be greater than or equal to FromFloorNumber");

        return new BuildingAppraisalSurface
        {
            //Id = Guid.CreateVersion7(),
            BuildingAppraisalDetailId = buildingAppraisalDetailId,
            FromFloorNumber = fromFloorNumber,
            ToFloorNumber = toFloorNumber,
            FloorType = floorType,
            FloorStructureType = floorStructureType,
            FloorStructureTypeOther = floorStructureType == "99" ? floorStructureTypeOther : null,
            FloorSurfaceType = floorSurfaceType,
            FloorSurfaceTypeOther = floorSurfaceType == "99" ? floorSurfaceTypeOther : null
        };
    }

    public void Update(
        int fromFloorNumber,
        int toFloorNumber,
        string? floorType = null,
        string? floorStructureType = null,
        string? floorStructureTypeOther = null,
        string? floorSurfaceType = null,
        string? floorSurfaceTypeOther = null)
    {
        if (toFloorNumber < fromFloorNumber)
            throw new ArgumentException("ToFloorNumber must be greater than or equal to FromFloorNumber");

        FromFloorNumber = fromFloorNumber;
        ToFloorNumber = toFloorNumber;
        FloorType = floorType;
        FloorStructureType = floorStructureType;
        FloorStructureTypeOther = floorStructureType == "99" ? floorStructureTypeOther : null;
        FloorSurfaceType = floorSurfaceType;
        FloorSurfaceTypeOther = floorSurfaceType == "99" ? floorSurfaceTypeOther : null;
    }
}