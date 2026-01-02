namespace Appraisal.AppraisalProperties.ValueObjects;

public class BuildingWall : ValueObject
{
    public string? InteriorWall { get; }
    public string? InteriorWallOther { get; }
    public string? ExteriorWall { get; }
    public string? ExteriorWallOther { get; }

    private BuildingWall(
        string? interiorWall,
        string? interiorWallOther,
        string? exteriorWall,
        string? exteriorWallOther
    )
    {
        InteriorWall = interiorWall;
        InteriorWallOther = interiorWallOther;
        ExteriorWall = exteriorWall;
        ExteriorWallOther = exteriorWallOther;
    }

    public static BuildingWall Create(
        string? interiorWall,
        string? interiorWallOther,
        string? exteriorWall,
        string? exteriorWallOther
    )
    {
        return new BuildingWall(interiorWall, interiorWallOther, exteriorWall, exteriorWallOther);
    }
}
