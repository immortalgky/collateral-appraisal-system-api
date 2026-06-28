namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Profit Rent pricing analysis — 1:1 child of PricingAnalysisMethod.
/// Stores inputs and computed results for profit rent PV calculation.
/// </summary>
public class ProfitRentAnalysis : Entity<Guid>
{
    private readonly List<ProfitRentGrowthPeriod> _growthPeriods = [];
    private readonly List<ProfitRentCalculationDetail> _tableRows = [];

    public IReadOnlyList<ProfitRentGrowthPeriod> GrowthPeriods => _growthPeriods.AsReadOnly();
    public IReadOnlyList<ProfitRentCalculationDetail> TableRows => _tableRows.AsReadOnly();

    public Guid PricingMethodId { get; private set; }

    // Input fields
    public decimal MarketRentalFeePerSqWa { get; private set; }
    public string GrowthRateType { get; private set; } = null!; // "Frequency" or "Period"
    public decimal GrowthRatePercent { get; private set; }
    public int GrowthIntervalYears { get; private set; }
    public decimal DiscountRate { get; private set; }
    public bool IncludeBuildingCost { get; private set; } // UI flag — stored for display, not used in PV calculation
    public decimal? EstimatePriceRounded { get; private set; }

    // Computed fields
    public decimal TotalMarketRentalFee { get; private set; }
    public decimal TotalContractRentalFee { get; private set; }
    public decimal TotalReturnsFromLease { get; private set; }
    public decimal TotalPresentValue { get; private set; }

    private ProfitRentAnalysis()
    {
        // For EF Core
    }

    public static ProfitRentAnalysis Create(Guid pricingMethodId)
    {
        return new ProfitRentAnalysis
        {
            Id = Guid.CreateVersion7(),
            PricingMethodId = pricingMethodId,
            GrowthRateType = "Frequency"
        };
    }

    /// <summary>Deep-clone for CI carry-forward — copies scalar inputs/computed values + child collections.</summary>
    public static ProfitRentAnalysis CloneForMethod(ProfitRentAnalysis source, Guid newMethodId)
    {
        var clone = new ProfitRentAnalysis
        {
            Id = Guid.CreateVersion7(),
            PricingMethodId = newMethodId,
            MarketRentalFeePerSqWa = source.MarketRentalFeePerSqWa,
            GrowthRateType = source.GrowthRateType,
            GrowthRatePercent = source.GrowthRatePercent,
            GrowthIntervalYears = source.GrowthIntervalYears,
            DiscountRate = source.DiscountRate,
            IncludeBuildingCost = source.IncludeBuildingCost,
            EstimatePriceRounded = source.EstimatePriceRounded,
            TotalMarketRentalFee = source.TotalMarketRentalFee,
            TotalContractRentalFee = source.TotalContractRentalFee,
            TotalReturnsFromLease = source.TotalReturnsFromLease,
            TotalPresentValue = source.TotalPresentValue
        };

        foreach (var p in source.GrowthPeriods)
            clone._growthPeriods.Add(ProfitRentGrowthPeriod.CloneForAnalysis(p, clone.Id));
        foreach (var r in source.TableRows)
            clone._tableRows.Add(ProfitRentCalculationDetail.CloneForAnalysis(r, clone.Id));

        return clone;
    }

    public void Update(
        decimal marketRentalFeePerSqWa,
        string growthRateType,
        decimal growthRatePercent,
        int growthIntervalYears,
        decimal discountRate,
        bool includeBuildingCost,
        decimal? estimatePriceRounded)
    {
        var validTypes = new[] { "Frequency", "Period" };
        if (!validTypes.Contains(growthRateType))
            throw new ArgumentException("GrowthRateType must be 'Frequency' or 'Period'");

        MarketRentalFeePerSqWa = marketRentalFeePerSqWa;
        GrowthRateType = growthRateType;
        GrowthRatePercent = growthRatePercent;
        GrowthIntervalYears = growthIntervalYears;
        DiscountRate = discountRate;
        IncludeBuildingCost = includeBuildingCost;
        EstimatePriceRounded = estimatePriceRounded;
    }

    public void SetComputedValues(
        decimal totalMarketRentalFee,
        decimal totalContractRentalFee,
        decimal totalReturnsFromLease,
        decimal totalPresentValue)
    {
        TotalMarketRentalFee = totalMarketRentalFee;
        TotalContractRentalFee = totalContractRentalFee;
        TotalReturnsFromLease = totalReturnsFromLease;
        TotalPresentValue = totalPresentValue;
    }

    public ProfitRentGrowthPeriod AddGrowthPeriod(int fromYear, int toYear, decimal growthRatePercent)
    {
        var period = ProfitRentGrowthPeriod.Create(Id, fromYear, toYear, growthRatePercent);
        _growthPeriods.Add(period);
        return period;
    }

    public void ClearGrowthPeriods()
    {
        _growthPeriods.Clear();
    }

    public void AddTableRow(ProfitRentCalculationDetail row)
    {
        _tableRows.Add(row);
    }

    public void ClearTableRows()
    {
        _tableRows.Clear();
    }
}
