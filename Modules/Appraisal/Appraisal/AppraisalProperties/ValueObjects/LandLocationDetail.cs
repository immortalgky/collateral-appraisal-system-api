namespace Appraisal.AppraisalProperties.ValueObjects;

public class LandLocationDetail : ValueObject
{
    public string? LandLocation { get; }
    public string? LandCheck { get; }
    public string? LandCheckOther { get; }
    public string Street { get; }
    public string? Soi { get; }
    public decimal? Distance { get; }
    public string? Village { get; }
    public string? AddressLocation { get; }
    public string? LandShape { get; }
    public string? UrbanPlanningType { get; }
    public string? Location { get; }
    public string? PlotLocation { get; }
    public string? PlotLocationOther { get; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "SonarQube",
        "S107:Methods should not have too many parameters"
    )]
    private LandLocationDetail(
        string? landLocation,
        string? landCheck,
        string? landCheckOther,
        string street,
        string? soi,
        decimal? distance,
        string? village,
        string? addressLocation,
        string? landShape,
        string? urbanPlanningType,
        string? location,
        string? plotLocation,
        string? plotLocationOther
    )
    {
        LandLocation = landLocation;
        LandCheck = landCheck;
        LandCheckOther = landCheckOther;
        Street = street;
        Soi = soi;
        Distance = distance;
        Village = village;
        AddressLocation = addressLocation;
        LandShape = landShape;
        UrbanPlanningType = urbanPlanningType;
        Location = location;
        PlotLocation = plotLocation;
        PlotLocationOther = plotLocationOther;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "SonarQube",
        "S107:Methods should not have too many parameters"
    )]
    public static LandLocationDetail Create(
        string? landLocation,
        string? landCheck,
        string? landCheckOther,
        string street,
        string? soi,
        decimal? distance,
        string? village,
        string? addressLocation,
        string? landShape,
        string? urbanPlanningType,
        string? location,
        string? plotLocation,
        string? plotLocationOther
    )
    {
        return new LandLocationDetail(
            landLocation,
            landCheck,
            landCheckOther,
            street,
            soi,
            distance,
            village,
            addressLocation,
            landShape,
            urbanPlanningType,
            location,
            plotLocation,
            plotLocationOther
        );
    }
}
