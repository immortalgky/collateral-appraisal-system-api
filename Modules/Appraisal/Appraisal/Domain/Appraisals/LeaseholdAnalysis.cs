namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Leasehold pricing analysis — 1:1 child of PricingAnalysisMethod.
/// Stores inputs and computed results for leasehold PV calculation.
/// </summary>
public class LeaseholdAnalysis : Entity<Guid>
{
    private readonly List<LeaseholdLandGrowthPeriod> _landGrowthPeriods = [];
    private readonly List<LeaseholdCalculationDetail> _tableRows = [];

    public IReadOnlyList<LeaseholdLandGrowthPeriod> LandGrowthPeriods => _landGrowthPeriods.AsReadOnly();
    public IReadOnlyList<LeaseholdCalculationDetail> TableRows => _tableRows.AsReadOnly();

    public Guid PricingMethodId { get; private set; }

    // Input fields
    public decimal LandValuePerSqWa { get; private set; }
    public string LandGrowthRateType { get; private set; } = null!; // "Frequency" or "Period"
    public decimal LandGrowthRatePercent { get; private set; }
    public int LandGrowthIntervalYears { get; private set; }
    public decimal ConstructionCostIndex { get; private set; }
    public decimal InitialBuildingValue { get; private set; }
    public decimal DepreciationRate { get; private set; }
    public int DepreciationIntervalYears { get; private set; }
    public int BuildingCalcStartYear { get; private set; }
    public decimal DiscountRate { get; private set; }

    // Computed fields
    public decimal TotalIncomeOverLeaseTerm { get; private set; }
    public decimal ValueAtLeaseExpiry { get; private set; }

    // Partial usage fields
    public bool IsPartialUsage { get; private set; }
    public decimal? PartialRai { get; private set; }
    public decimal? PartialNgan { get; private set; }
    public decimal? PartialWa { get; private set; }
    public decimal? PartialLandArea { get; private set; }
    public decimal? PricePerSqWa { get; private set; }
    public decimal? PartialLandPrice { get; private set; }
    public decimal? EstimateNetPrice { get; private set; }
    public decimal? EstimatePriceRounded { get; private set; }

    private LeaseholdAnalysis()
    {
        // For EF Core
    }

    public static LeaseholdAnalysis Create(Guid pricingMethodId)
    {
        return new LeaseholdAnalysis
        {
            Id = Guid.CreateVersion7(),
            PricingMethodId = pricingMethodId,
            LandGrowthRateType = "Frequency"
        };
    }

    /// <summary>Deep-clone for CI carry-forward — copies scalar inputs/computed values + child collections.</summary>
    public static LeaseholdAnalysis CloneForMethod(LeaseholdAnalysis source, Guid newMethodId)
    {
        var clone = new LeaseholdAnalysis
        {
            Id = Guid.CreateVersion7(),
            PricingMethodId = newMethodId,
            LandValuePerSqWa = source.LandValuePerSqWa,
            LandGrowthRateType = source.LandGrowthRateType,
            LandGrowthRatePercent = source.LandGrowthRatePercent,
            LandGrowthIntervalYears = source.LandGrowthIntervalYears,
            ConstructionCostIndex = source.ConstructionCostIndex,
            InitialBuildingValue = source.InitialBuildingValue,
            DepreciationRate = source.DepreciationRate,
            DepreciationIntervalYears = source.DepreciationIntervalYears,
            BuildingCalcStartYear = source.BuildingCalcStartYear,
            DiscountRate = source.DiscountRate,
            TotalIncomeOverLeaseTerm = source.TotalIncomeOverLeaseTerm,
            ValueAtLeaseExpiry = source.ValueAtLeaseExpiry,
            IsPartialUsage = source.IsPartialUsage,
            PartialRai = source.PartialRai,
            PartialNgan = source.PartialNgan,
            PartialWa = source.PartialWa,
            PartialLandArea = source.PartialLandArea,
            PricePerSqWa = source.PricePerSqWa,
            PartialLandPrice = source.PartialLandPrice,
            EstimateNetPrice = source.EstimateNetPrice,
            EstimatePriceRounded = source.EstimatePriceRounded,
        };

        foreach (var p in source.LandGrowthPeriods)
            clone._landGrowthPeriods.Add(LeaseholdLandGrowthPeriod.CloneForAnalysis(p, clone.Id));
        foreach (var r in source.TableRows)
            clone._tableRows.Add(LeaseholdCalculationDetail.CloneForAnalysis(r, clone.Id));

        return clone;
    }

    public void Update(
        decimal landValuePerSqWa,
        string landGrowthRateType,
        decimal landGrowthRatePercent,
        int landGrowthIntervalYears,
        decimal constructionCostIndex,
        decimal initialBuildingValue,
        decimal depreciationRate,
        int depreciationIntervalYears,
        int buildingCalcStartYear,
        decimal discountRate)
    {
        var validTypes = new[] { "Frequency", "Period" };
        if (!validTypes.Contains(landGrowthRateType))
            throw new ArgumentException("LandGrowthRateType must be 'Frequency' or 'Period'");

        LandValuePerSqWa = landValuePerSqWa;
        LandGrowthRateType = landGrowthRateType;
        LandGrowthRatePercent = landGrowthRatePercent;
        LandGrowthIntervalYears = landGrowthIntervalYears;
        ConstructionCostIndex = constructionCostIndex;
        InitialBuildingValue = initialBuildingValue;
        DepreciationRate = depreciationRate;
        DepreciationIntervalYears = depreciationIntervalYears;
        BuildingCalcStartYear = buildingCalcStartYear;
        DiscountRate = discountRate;
    }

    public void SetComputedValues(
        decimal totalIncomeOverLeaseTerm,
        decimal valueAtLeaseExpiry)
    {
        TotalIncomeOverLeaseTerm = totalIncomeOverLeaseTerm;
        ValueAtLeaseExpiry = valueAtLeaseExpiry;
    }

    public void SetPartialUsage(
        bool isPartialUsage,
        decimal? partialRai,
        decimal? partialNgan,
        decimal? partialWa,
        decimal? partialLandArea,
        decimal? pricePerSqWa,
        decimal? partialLandPrice,
        decimal? estimateNetPrice,
        decimal? estimatePriceRounded)
    {
        IsPartialUsage = isPartialUsage;
        PartialRai = partialRai;
        PartialNgan = partialNgan;
        PartialWa = partialWa;
        PartialLandArea = partialLandArea;
        PricePerSqWa = pricePerSqWa;
        PartialLandPrice = partialLandPrice;
        EstimateNetPrice = estimateNetPrice;
        EstimatePriceRounded = estimatePriceRounded;
    }

    public LeaseholdLandGrowthPeriod AddLandGrowthPeriod(int fromYear, int toYear, decimal growthRatePercent)
    {
        var period = LeaseholdLandGrowthPeriod.Create(Id, fromYear, toYear, growthRatePercent);
        _landGrowthPeriods.Add(period);
        return period;
    }

    public void ClearLandGrowthPeriods()
    {
        _landGrowthPeriods.Clear();
    }

    public void AddTableRow(LeaseholdCalculationDetail row)
    {
        _tableRows.Add(row);
    }

    public void ClearTableRows()
    {
        _tableRows.Clear();
    }
}
