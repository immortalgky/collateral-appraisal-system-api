namespace Appraisal.Domain.Appraisals;

public class VillageUnitPrice : Entity<Guid>
{
    public Guid VillageUnitId { get; private set; }

    // Location Flags
    public bool IsCorner { get; private set; }
    public bool IsEdge { get; private set; }
    public bool IsNearGarden { get; private set; }
    public bool IsOther { get; private set; }

    // Calculated Values
    public decimal? LandIncreaseDecreaseAmount { get; private set; }
    public decimal? AdjustPriceLocation { get; private set; }
    public decimal? StandardPrice { get; private set; }
    public decimal? TotalAppraisalValue { get; private set; }
    public decimal? TotalAppraisalValueRounded { get; private set; }
    public decimal? ForceSellingPrice { get; private set; }
    public decimal? CoverageAmount { get; private set; }

    private VillageUnitPrice()
    {
    }

    public static VillageUnitPrice Create(Guid villageUnitId)
    {
        return new VillageUnitPrice
        {
            Id = Guid.CreateVersion7(),
            VillageUnitId = villageUnitId,
            IsCorner = false,
            IsEdge = false,
            IsNearGarden = false,
            IsOther = false
        };
    }

    public void UpdateLocationFlags(bool isCorner, bool isEdge, bool isNearGarden, bool isOther)
    {
        IsCorner = isCorner;
        IsEdge = isEdge;
        IsNearGarden = isNearGarden;
        IsOther = isOther;
    }

    public void UpdateCalculatedValues(
        decimal? landIncreaseDecreaseAmount,
        decimal? adjustPriceLocation,
        decimal? standardPrice,
        decimal? totalAppraisalValue,
        decimal? totalAppraisalValueRounded,
        decimal? forceSellingPrice,
        decimal? coverageAmount)
    {
        LandIncreaseDecreaseAmount = landIncreaseDecreaseAmount;
        AdjustPriceLocation = adjustPriceLocation;
        StandardPrice = standardPrice;
        TotalAppraisalValue = totalAppraisalValue;
        TotalAppraisalValueRounded = totalAppraisalValueRounded;
        ForceSellingPrice = forceSellingPrice;
        CoverageAmount = coverageAmount;
    }
}
