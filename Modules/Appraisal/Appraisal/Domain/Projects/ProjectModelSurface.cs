namespace Appraisal.Domain.Projects;

/// <summary>
/// Floor-by-floor surface details for LandAndBuilding project model buildings.
/// Populated only when the owning ProjectModel belongs to a LandAndBuilding project.
/// </summary>
public class ProjectModelSurface : Entity<Guid>
{
    public Guid ProjectModelId { get; private set; }

    // Floor Range
    public int FromFloorNumber { get; private set; }
    public int ToFloorNumber { get; private set; }

    // Floor Details
    public string? FloorType { get; private set; }
    public string? FloorStructureType { get; private set; }
    public string? FloorStructureTypeOther { get; private set; }
    public string? FloorSurfaceType { get; private set; }
    public string? FloorSurfaceTypeOther { get; private set; }

    private ProjectModelSurface()
    {
    }

    public static ProjectModelSurface Create(
        Guid projectModelId,
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

        return new ProjectModelSurface
        {
            Id = Guid.CreateVersion7(),
            ProjectModelId = projectModelId,
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
