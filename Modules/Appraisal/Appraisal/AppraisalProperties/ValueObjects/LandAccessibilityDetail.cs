namespace Appraisal.AppraisalProperties.ValueObjects;

public class LandAccessibilityDetail : ValueObject
{
    public FrontageRoad FrontageRoad { get; } = default!;
    public string? RoadSurface { get; }
    public string? RoadSurfaceOther { get; }
    public string? PublicUtility { get; }
    public string? PublicUtilityOther { get; }
    public string? LandUse { get; }
    public string? LandUseOther { get; }
    public string? LandEntranceExit { get; }
    public string? LandEntranceExitOther { get; }
    public string? Transportation { get; }
    public string? TransportationOther { get; }

    private LandAccessibilityDetail() { }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private LandAccessibilityDetail(
        FrontageRoad frontageRoad,
        string? roadSurface,
        string? roadSurfaceOther,
        string? publicUtility,
        string? publicUtilityOther,
        string? landUse,
        string? landUseOther,
        string? landEntranceExit,
        string? landEntranceExitOther,
        string? transportation,
        string? transportationOther
    )
    {
        FrontageRoad = frontageRoad;
        RoadSurface = roadSurface;
        RoadSurfaceOther = roadSurfaceOther;
        PublicUtility = publicUtility;
        PublicUtilityOther = publicUtilityOther;
        LandUse = landUse;
        LandUseOther = landUseOther;
        LandEntranceExit = landEntranceExit;
        LandEntranceExitOther = landEntranceExitOther;
        Transportation = transportation;
        TransportationOther = transportationOther;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static LandAccessibilityDetail Create(
        FrontageRoad frontageRoad,
        string? roadSurface,
        string? roadSurfaceOther,
        string? publicUtility,
        string? publicUtilityOther,
        string? landUse,
        string? landUseOther,
        string? landEntranceExit,
        string? landEntranceExitOther,
        string? transportation,
        string? transportationOther
    )
    {
        return new LandAccessibilityDetail(
            frontageRoad,
            roadSurface,
            roadSurfaceOther,
            publicUtility,
            publicUtilityOther,
            landUse,
            landUseOther,
            landEntranceExit,
            landEntranceExitOther,
            transportation,
            transportationOther
        );
    }
}
