namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Floor-by-floor surface details for building appraisals.
/// </summary>
public class BuildingAppraisalSurface : Entity<Guid>
{
    public Guid AppraisalPropertyId { get; private set; }

    // Floor Range
    public int FromFloorNo { get; private set; }
    public int ToFloorNo { get; private set; }

    // Floor Details
    public string? FloorType { get; private set; } // Normal, Mezzanine, HighFoundation, Rooftop
    public string? FloorStructure { get; private set; } // KSL, Wood, SmartBoard, Others
    public string? FloorStructureOther { get; private set; }
    public string? FloorSurface { get; private set; } // Granite, Wood, Tiles, Ceramic, Marble, Others
    public string? FloorSurfaceOther { get; private set; }

    private BuildingAppraisalSurface()
    {
    }

    public static BuildingAppraisalSurface Create(
        Guid appraisalPropertyId,
        int fromFloorNo,
        int toFloorNo)
    {
        if (toFloorNo < fromFloorNo)
            throw new ArgumentException("ToFloorNo must be greater than or equal to FromFloorNo");

        return new BuildingAppraisalSurface
        {
            Id = Guid.NewGuid(),
            AppraisalPropertyId = appraisalPropertyId,
            FromFloorNo = fromFloorNo,
            ToFloorNo = toFloorNo
        };
    }

    public void SetFloorDetails(
        string? floorType,
        string? floorStructure,
        string? floorStructureOther,
        string? floorSurface,
        string? floorSurfaceOther)
    {
        FloorType = floorType;
        FloorStructure = floorStructure;
        FloorStructureOther = floorStructure == "Others" ? floorStructureOther : null;
        FloorSurface = floorSurface;
        FloorSurfaceOther = floorSurface == "Others" ? floorSurfaceOther : null;
    }
}