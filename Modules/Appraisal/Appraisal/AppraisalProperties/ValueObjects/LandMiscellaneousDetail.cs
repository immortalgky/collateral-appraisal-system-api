namespace Appraisal.AppraisalProperties.ValueObjects;

public class LandMiscellaneousDetail : ValueObject
{
    public decimal? PondArea { get; }
    public decimal? DepthPit { get; }
    public string? HasBuilding { get; }
    public string? HasBuildingOther { get; }

    private LandMiscellaneousDetail(
        decimal? pondArea,
        decimal? depthPit,
        string? hasBuilding,
        string? hasBuildingOther
    )
    {
        PondArea = pondArea;
        DepthPit = depthPit;
        HasBuilding = hasBuilding;
        HasBuildingOther = hasBuildingOther;
    }

    public static LandMiscellaneousDetail Create(
        decimal? pondArea,
        decimal? depthPit,
        string? hasBuilding,
        string? hasBuildingOther
    )
    {
        return new LandMiscellaneousDetail(pondArea, depthPit, hasBuilding, hasBuildingOther);
    }
}
