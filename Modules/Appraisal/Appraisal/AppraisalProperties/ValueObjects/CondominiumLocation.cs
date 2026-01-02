namespace Appraisal.AppraisalProperties.ValueObjects;

public class CondominiumLocation : ValueObject
{
    public bool? CondoLocation { get; }
    public string? Street { get; }
    public string? Soi { get; }
    public decimal? Distance { get; }
    public decimal? RoadWidth { get; }
    public decimal? RightOfWay { get; }
    public string? RoadSurface { get; }
    public string? PublicUtility { get; }
    public string? PublicUtilityOther { get; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private CondominiumLocation(
        bool? condoLocation,
        string? street,
        string? soi,
        decimal? distance,
        decimal? roadWidth,
        decimal? rightOfWay,
        string? roadSurface,
        string? publicUtility,
        string? publicUtilityOther
    )
    {
        CondoLocation = condoLocation;
        Street = street;
        Soi = soi;
        Distance = distance;
        RoadWidth = roadWidth;
        RightOfWay = rightOfWay;
        RoadSurface = roadSurface;
        PublicUtility = publicUtility;
        PublicUtilityOther = publicUtilityOther;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static CondominiumLocation Create(
        bool? condoLocation,
        string? street,
        string? soi,
        decimal? distance,
        decimal? roadWidth,
        decimal? rightOfWay,
        string? roadSurface,
        string? publicUtility,
        string? publicUtilityOther
    )
    {
        return new CondominiumLocation(
            condoLocation,
            street,
            soi,
            distance,
            roadWidth,
            rightOfWay,
            roadSurface,
            publicUtility,
            publicUtilityOther
        );
    }
}
