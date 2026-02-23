namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Floor-by-floor surface details for building appraisals.
/// </summary>
public class BuildingAppraisalSurface : Entity<Guid>
{
    public Guid AppraisalPropertyId { get; private set; }

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
        Guid appraisalPropertyId,
        int fromFloorNumber,
        int toFloorNumber)
    {
        if (toFloorNumber < fromFloorNumber)
            throw new ArgumentException("ToFloorNo must be greater than or equal to FromFloorNo");

        return new BuildingAppraisalSurface
        {
            Id = Guid.CreateVersion7(),
            AppraisalPropertyId = appraisalPropertyId,
            FromFloorNumber = fromFloorNumber,
            ToFloorNumber = toFloorNumber
        };
    }

    public void SetFloorDetails(
        string? floorType,
        string? floorStructureType,
        string? floorStructureTypeOther,
        string? floorSurfaceType,
        string? floorSurfaceTypeOther)
    {
        FloorType = floorType;
        FloorStructureType = floorStructureType;
        FloorStructureTypeOther = floorStructureType == "Others" ? floorStructureTypeOther : null;
        FloorSurfaceType = floorSurfaceType;
        FloorSurfaceTypeOther = floorSurfaceType == "Others" ? floorSurfaceTypeOther : null;
    }
}