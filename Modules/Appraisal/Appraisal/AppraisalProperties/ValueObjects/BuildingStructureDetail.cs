namespace Appraisal.AppraisalProperties.ValueObjects;

public class BuildingStructureDetail : ValueObject
{
    public BuildingConstructionStyle BuildingConstructionStyle { get; } = default!;
    public BuildingGeneralStructure BuildingGeneralStructure { get; } = default!;
    public BuildingRoofFrame BuildingRoofFrame { get; } = default!;
    public BuildingRoof BuildingRoof { get; } = default!;
    public BuildingCeiling BuildingCeiling { get; } = default!;
    public BuildingWall BuildingWall { get; } = default!;
    public BuildingFence BuildingFence { get; } = default!;
    public BuildingConstructionType ConstType { get; } = default!;

    private BuildingStructureDetail() { }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private BuildingStructureDetail(
        BuildingConstructionStyle buildingConstructionStyle,
        BuildingGeneralStructure buildingGeneralStructure,
        BuildingRoofFrame buildingRoofFrame,
        BuildingRoof buildingRoof,
        BuildingCeiling buildingCeiling,
        BuildingWall buildingWall,
        BuildingFence buildingFence,
        BuildingConstructionType constType
    )
    {
        BuildingConstructionStyle = buildingConstructionStyle;
        BuildingGeneralStructure = buildingGeneralStructure;
        BuildingRoofFrame = buildingRoofFrame;
        BuildingRoof = buildingRoof;
        BuildingCeiling = buildingCeiling;
        BuildingWall = buildingWall;
        BuildingFence = buildingFence;
        ConstType = constType;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static BuildingStructureDetail Create(
        BuildingConstructionStyle buildingConstructionStyle,
        BuildingGeneralStructure buildingGeneralStructure,
        BuildingRoofFrame buildingRoofFrame,
        BuildingRoof buildingRoof,
        BuildingCeiling buildingCeiling,
        BuildingWall buildingWall,
        BuildingFence buildingFence,
        BuildingConstructionType constType
    )
    {
        return new BuildingStructureDetail(
            buildingConstructionStyle,
            buildingGeneralStructure,
            buildingRoofFrame,
            buildingRoof,
            buildingCeiling,
            buildingWall,
            buildingFence,
            constType
        );
    }
}
