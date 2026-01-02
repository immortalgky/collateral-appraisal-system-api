using System;

namespace Request.Domain.RequestTitles;

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

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(BuildingType);
        if (UsableArea is null || UsableArea < 0)
            throw new ArgumentException("usableArea must be >= 0.");
        if (NumberOfBuilding is null || NumberOfBuilding < 0)
            throw new ArgumentException("numberOfBuilding must be >= 0.");
    }
}