namespace Appraisal.AppraisalProperties.ValueObjects;

public class BuildingTypeDetail : ValueObject
{
    public string BuildingType { get; } = default!;
    public string? BuildingTypeOther { get; }
    public short? TotalFloor { get; }

    private BuildingTypeDetail() { }

    private BuildingTypeDetail(string buildingType, string? buildingTypeOther, short? totalFloor)
    {
        BuildingType = buildingType;
        BuildingTypeOther = buildingTypeOther;
        TotalFloor = totalFloor;
    }

    public static BuildingTypeDetail Create(
        string buildingType,
        string? buildingTypeOther,
        short? totalFloor
    )
    {
        return new BuildingTypeDetail(buildingType, buildingTypeOther, totalFloor);
    }
}
