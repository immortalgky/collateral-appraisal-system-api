namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Floor-by-floor surface details for village model buildings.
/// Owned by VillageModel.
/// </summary>
public class VillageModelSurface : Entity<Guid>
{
    public Guid VillageModelId { get; private set; }

    // Floor Range
    public int FromFloorNumber { get; private set; }
    public int ToFloorNumber { get; private set; }

    // Floor Details
    public string? FloorType { get; private set; }
    public string? FloorStructureType { get; private set; }
    public string? FloorStructureTypeOther { get; private set; }
    public string? FloorSurfaceType { get; private set; }
    public string? FloorSurfaceTypeOther { get; private set; }

    private VillageModelSurface()
    {
    }

    public static VillageModelSurface Create(
        Guid villageModelId,
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

        return new VillageModelSurface
        {
            VillageModelId = villageModelId,
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
