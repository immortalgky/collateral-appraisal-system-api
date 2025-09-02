namespace Appraisal.AppraisalProperties.ValueObjects;

public class BuildingConstructionInformation : ValueObject
{
    public decimal? OriginalBuildingPct { get; }
    public decimal? UnderConstPct { get; }

    private BuildingConstructionInformation(decimal? originalBuildingPct, decimal? underConstPct)
    {
        OriginalBuildingPct = originalBuildingPct;
        UnderConstPct = underConstPct;
    }

    public static BuildingConstructionInformation Create(
        decimal? originalBuildingPct,
        decimal? underConstPct
    )
    {
        return new BuildingConstructionInformation(originalBuildingPct, underConstPct);
    }
}
