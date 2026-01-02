namespace Appraisal.AppraisalProperties.ValueObjects;

public class FrontageRoad : ValueObject
{
    public decimal? RoadWidth { get; }
    public decimal? RightOfWay { get; }
    public decimal? WideFrontageOfLand { get; }
    public decimal? NoOfSideFacingRoad { get; }
    public decimal? RoadPassInFrontOfLand { get; }
    public string? LandAccessibility { get; }
    public string? LandAccessibilityDesc { get; }

    private FrontageRoad(
        decimal? roadWidth,
        decimal? rightOfWay,
        decimal? wideFrontageOfLand,
        decimal? noOfSideFacingRoad,
        decimal? roadPassInFrontOfLand,
        string? landAccessibility,
        string? landAccessibilityDesc
    )
    {
        RoadWidth = roadWidth;
        RightOfWay = rightOfWay;
        WideFrontageOfLand = wideFrontageOfLand;
        NoOfSideFacingRoad = noOfSideFacingRoad;
        RoadPassInFrontOfLand = roadPassInFrontOfLand;
        LandAccessibility = landAccessibility;
        LandAccessibilityDesc = landAccessibilityDesc;
    }

    public static FrontageRoad Create(
        decimal? roadWidth,
        decimal? rightOfWay,
        decimal? wideFrontageOfLand,
        decimal? noOfSideFacingRoad,
        decimal? roadPassInFrontOfLand,
        string? landAccessibility,
        string? landAccessibilityDesc
    )
    {
        return new FrontageRoad(
            roadWidth,
            rightOfWay,
            wideFrontageOfLand,
            noOfSideFacingRoad,
            roadPassInFrontOfLand,
            landAccessibility,
            landAccessibilityDesc
        );
    }
}
