namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Building cost depreciation detail for village model buildings.
/// Owned by VillageModel (OwnsMany).
/// Supports two depreciation modes: "Period" (year-range rows) and "Gross" (single total %).
/// </summary>
public class VillageModelDepreciationDetail : Entity<Guid>
{
    public Guid VillageModelId { get; private set; }

    // Building Info
    public string? AreaDescription { get; private set; }
    public decimal Area { get; private set; }
    public short Year { get; private set; }
    public bool IsBuilding { get; private set; }

    // Pricing
    public decimal PricePerSqMBeforeDepreciation { get; private set; }
    public decimal PriceBeforeDepreciation { get; private set; }
    public decimal PricePerSqMAfterDepreciation { get; private set; }
    public decimal PriceAfterDepreciation { get; private set; }

    // Depreciation
    public string DepreciationMethod { get; private set; } = null!; // "Period" or "Gross"
    public decimal DepreciationYearPct { get; private set; }
    public decimal TotalDepreciationPct { get; private set; }
    public decimal PriceDepreciation { get; private set; }

    // Child periods (for "Period" mode)
    private readonly List<VillageModelDepreciationPeriod> _depreciationPeriods = [];
    public IReadOnlyList<VillageModelDepreciationPeriod> DepreciationPeriods => _depreciationPeriods.AsReadOnly();

    private VillageModelDepreciationDetail()
    {
    }

    public static VillageModelDepreciationDetail Create(
        Guid villageModelId,
        string depreciationMethod,
        string? areaDescription = null,
        decimal area = 0,
        short year = 0,
        bool isBuilding = true,
        decimal pricePerSqMBeforeDepreciation = 0,
        decimal priceBeforeDepreciation = 0,
        decimal pricePerSqMAfterDepreciation = 0,
        decimal priceAfterDepreciation = 0,
        decimal depreciationYearPct = 0,
        decimal totalDepreciationPct = 0,
        decimal priceDepreciation = 0)
    {
        ValidateDepreciationMethod(depreciationMethod);

        return new VillageModelDepreciationDetail
        {
            VillageModelId = villageModelId,
            DepreciationMethod = depreciationMethod,
            AreaDescription = areaDescription,
            Area = area,
            Year = year,
            IsBuilding = isBuilding,
            PricePerSqMBeforeDepreciation = pricePerSqMBeforeDepreciation,
            PriceBeforeDepreciation = priceBeforeDepreciation,
            PricePerSqMAfterDepreciation = pricePerSqMAfterDepreciation,
            PriceAfterDepreciation = priceAfterDepreciation,
            DepreciationYearPct = depreciationYearPct,
            TotalDepreciationPct = totalDepreciationPct,
            PriceDepreciation = priceDepreciation
        };
    }

    public void Update(
        string depreciationMethod,
        string? areaDescription = null,
        decimal area = 0,
        short year = 0,
        bool isBuilding = true,
        decimal pricePerSqMBeforeDepreciation = 0,
        decimal priceBeforeDepreciation = 0,
        decimal pricePerSqMAfterDepreciation = 0,
        decimal priceAfterDepreciation = 0,
        decimal depreciationYearPct = 0,
        decimal totalDepreciationPct = 0,
        decimal priceDepreciation = 0)
    {
        ValidateDepreciationMethod(depreciationMethod);

        DepreciationMethod = depreciationMethod;
        AreaDescription = areaDescription;
        Area = area;
        Year = year;
        IsBuilding = isBuilding;
        PricePerSqMBeforeDepreciation = pricePerSqMBeforeDepreciation;
        PriceBeforeDepreciation = priceBeforeDepreciation;
        PricePerSqMAfterDepreciation = pricePerSqMAfterDepreciation;
        PriceAfterDepreciation = priceAfterDepreciation;
        DepreciationYearPct = depreciationYearPct;
        TotalDepreciationPct = totalDepreciationPct;
        PriceDepreciation = priceDepreciation;
    }

    public VillageModelDepreciationPeriod AddPeriod(
        int atYear,
        int toYear,
        decimal depreciationPerYear,
        decimal totalDepreciationPct,
        decimal priceDepreciation)
    {
        var period = VillageModelDepreciationPeriod.Create(
            Id, atYear, toYear, depreciationPerYear, totalDepreciationPct, priceDepreciation);
        _depreciationPeriods.Add(period);
        return period;
    }

    public void ClearPeriods()
    {
        _depreciationPeriods.Clear();
    }

    private static void ValidateDepreciationMethod(string method)
    {
        if (method is not ("Period" or "Gross"))
            throw new ArgumentException("DepreciationMethod must be 'Period' or 'Gross'");
    }
}
