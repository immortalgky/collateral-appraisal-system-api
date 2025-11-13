using System;

namespace Request.RequestTitles.ValueObjects;

public class BuildingInfo : ValueObject
{
    public string? BuildingType { get; }
    public decimal? UsableArea { get; }
    public int? NumberOfBuilding { get; }

    private BuildingInfo(string? buildingType, decimal? usableArea, int? numberOfBuilding)
    {
        BuildingType = buildingType;
        UsableArea = usableArea;
        NumberOfBuilding = numberOfBuilding;
    }

    public static BuildingInfo Create(string? buildingType, decimal? usableArea, int? numberOfBuilding)
    {
        return new BuildingInfo(buildingType, usableArea, numberOfBuilding);
    }

    public BuildingInfo Update(string? buildingType, decimal? usableArea, int? numberOfBuilding)
    {
        return new BuildingInfo(buildingType, usableArea, numberOfBuilding);
    }
}
