namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Stores a single row of the leasehold calculation table.
/// Child of LeaseholdAnalysis — regenerated on every save.
/// </summary>
public class LeaseholdCalculationDetail : Entity<Guid>
{
    public Guid LeaseholdAnalysisId { get; private set; }
    public int DisplaySequence { get; private set; }
    public decimal Year { get; private set; }
    public decimal LandValue { get; private set; }
    public decimal LandGrowthPercent { get; private set; }
    public decimal BuildingValue { get; private set; }
    public decimal DepreciationAmount { get; private set; }
    public decimal DepreciationPercent { get; private set; }
    public decimal BuildingAfterDepreciation { get; private set; }
    public decimal TotalLandAndBuilding { get; private set; }
    public decimal RentalIncome { get; private set; }
    public decimal PvFactor { get; private set; }
    public decimal NetCurrentRentalIncome { get; private set; }

    private LeaseholdCalculationDetail() { }

    public static LeaseholdCalculationDetail Create(
        Guid leaseholdAnalysisId,
        int displaySequence,
        decimal year,
        decimal landValue,
        decimal landGrowthPercent,
        decimal buildingValue,
        decimal depreciationAmount,
        decimal depreciationPercent,
        decimal buildingAfterDepreciation,
        decimal totalLandAndBuilding,
        decimal rentalIncome,
        decimal pvFactor,
        decimal netCurrentRentalIncome)
    {
        return new LeaseholdCalculationDetail
        {
            Id = Guid.CreateVersion7(),
            LeaseholdAnalysisId = leaseholdAnalysisId,
            DisplaySequence = displaySequence,
            Year = year,
            LandValue = landValue,
            LandGrowthPercent = landGrowthPercent,
            BuildingValue = buildingValue,
            DepreciationAmount = depreciationAmount,
            DepreciationPercent = depreciationPercent,
            BuildingAfterDepreciation = buildingAfterDepreciation,
            TotalLandAndBuilding = totalLandAndBuilding,
            RentalIncome = rentalIncome,
            PvFactor = pvFactor,
            NetCurrentRentalIncome = netCurrentRentalIncome,
        };
    }

    /// <summary>Deep-clone for CI carry-forward.</summary>
    public static LeaseholdCalculationDetail CloneForAnalysis(LeaseholdCalculationDetail source, Guid newAnalysisId)
    {
        return new LeaseholdCalculationDetail
        {
            Id = Guid.CreateVersion7(),
            LeaseholdAnalysisId = newAnalysisId,
            DisplaySequence = source.DisplaySequence,
            Year = source.Year,
            LandValue = source.LandValue,
            LandGrowthPercent = source.LandGrowthPercent,
            BuildingValue = source.BuildingValue,
            DepreciationAmount = source.DepreciationAmount,
            DepreciationPercent = source.DepreciationPercent,
            BuildingAfterDepreciation = source.BuildingAfterDepreciation,
            TotalLandAndBuilding = source.TotalLandAndBuilding,
            RentalIncome = source.RentalIncome,
            PvFactor = source.PvFactor,
            NetCurrentRentalIncome = source.NetCurrentRentalIncome,
        };
    }
}
