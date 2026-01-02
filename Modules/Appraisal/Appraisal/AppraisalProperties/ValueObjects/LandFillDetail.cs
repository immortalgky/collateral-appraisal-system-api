namespace Appraisal.AppraisalProperties.ValueObjects;

public class LandFillDetail : ValueObject
{
    public string? LandFill { get; }
    public decimal? LandFillPct { get; }
    public decimal? SoilLevel { get; }

    private LandFillDetail(string? landFill, decimal? landFillPct, decimal? soilLevel)
    {
        LandFill = landFill;
        LandFillPct = landFillPct;
        SoilLevel = soilLevel;
    }

    public static LandFillDetail Create(string? landFill, decimal? landFillPct, decimal? soilLevel)
    {
        return new LandFillDetail(landFill, landFillPct, soilLevel);
    }
}
