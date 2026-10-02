namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Thai-style building cost depreciation detail.
/// Owned by BuildingAppraisalDetail (OwnsMany).
/// Supports two depreciation modes: "Period" (year-range rows) and "Gross" (single total %).
/// </summary>
public class BuildingDepreciationDetail : Entity<Guid>
{
    public Guid BuildingAppraisalDetailId { get; private set; }

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
    private readonly List<BuildingDepreciationPeriod> _depreciationPeriods = [];
    public IReadOnlyList<BuildingDepreciationPeriod> DepreciationPeriods => _depreciationPeriods.AsReadOnly();

    private BuildingDepreciationDetail()
    {
    }

    public static BuildingDepreciationDetail Create(
        Guid buildingAppraisalDetailId,
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

        return new BuildingDepreciationDetail
        {
            //Id = Guid.CreateVersion7(),
            BuildingAppraisalDetailId = buildingAppraisalDetailId,
            DepreciationMethod = depreciationMethod,
            AreaDescription = areaDescription,
            Area = Stored(area, 4),
            Year = year,
            IsBuilding = isBuilding,
            PricePerSqMBeforeDepreciation = Stored(pricePerSqMBeforeDepreciation, 2),
            PriceBeforeDepreciation = Stored(priceBeforeDepreciation, 2),
            PricePerSqMAfterDepreciation = Stored(pricePerSqMAfterDepreciation, 2),
            PriceAfterDepreciation = Stored(priceAfterDepreciation, 2),
            DepreciationYearPct = Stored(depreciationYearPct, 4),
            TotalDepreciationPct = Stored(totalDepreciationPct, 4),
            PriceDepreciation = Stored(priceDepreciation, 2)
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
        Area = Stored(area, 4);
        Year = year;
        IsBuilding = isBuilding;
        PricePerSqMBeforeDepreciation = Stored(pricePerSqMBeforeDepreciation, 2);
        PriceBeforeDepreciation = Stored(priceBeforeDepreciation, 2);
        PricePerSqMAfterDepreciation = Stored(pricePerSqMAfterDepreciation, 2);
        PriceAfterDepreciation = Stored(priceAfterDepreciation, 2);
        DepreciationYearPct = Stored(depreciationYearPct, 4);
        TotalDepreciationPct = Stored(totalDepreciationPct, 4);
        PriceDepreciation = Stored(priceDepreciation, 2);
    }

    public BuildingDepreciationPeriod AddPeriod(
        int atYear,
        int toYear,
        decimal depreciationPerYear,
        decimal totalDepreciationPct,
        decimal priceDepreciation)
    {
        var period = BuildingDepreciationPeriod.Create(
            Id, atYear, toYear, depreciationPerYear, totalDepreciationPct, priceDepreciation);
        _depreciationPeriods.Add(period);
        return period;
    }

    public void ClearPeriods()
    {
        _depreciationPeriods.Clear();
    }

    /// <summary>
    /// The value as its column stores it: money is decimal(18,2), area decimal(18,4), percentages
    /// decimal(7,4). The property form computes these in floating point to more places (÷ area, × %,
    /// 3 × 1.1 = 3.3000000000000003), and SQL Server rounds half away from zero on the way in. Holding the
    /// same figure here keeps the data-correction snapshot equal to what is stored — otherwise every
    /// correction of such a row logs 7754.93 → 7754.9325 for a value that never changes. The scale is
    /// padded too (5000 → 5000.00) so the audit prints the new figure the way it prints the stored one.
    /// </summary>
    internal static decimal Stored(decimal value, int places)
    {
        var rounded = Math.Round(value, places, MidpointRounding.AwayFromZero);
        // Float noise just below zero (a fully depreciated row posts -1.455E-10) rounds to a negative zero.
        return (rounded == 0m ? 0m : rounded) + new decimal(0, 0, 0, false, (byte)places);
    }

    private static void ValidateDepreciationMethod(string method)
    {
        if (method is not ("Period" or "Gross"))
            throw new ArgumentException("DepreciationMethod must be 'Period' or 'Gross'");
    }
}