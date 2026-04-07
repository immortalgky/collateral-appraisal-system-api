namespace Appraisal.Domain.Appraisals;

public class CondoUnitPrice : Entity<Guid>
{
    public Guid CondoUnitId { get; private set; }

    // Location Flags
    public bool IsCorner { get; private set; }
    public bool IsEdge { get; private set; }
    public bool IsPoolView { get; private set; }
    public bool IsSouth { get; private set; }
    public bool IsOther { get; private set; }

    // Calculated Values
    public decimal? AdjustPriceLocation { get; private set; }
    public decimal? StandardPrice { get; private set; }
    public decimal? PriceIncrementPerFloor { get; private set; }
    public decimal? TotalAppraisalValue { get; private set; }
    public decimal? TotalAppraisalValueRounded { get; private set; }
    public decimal? ForceSellingPrice { get; private set; }
    public decimal? CoverageAmount { get; private set; }

    private CondoUnitPrice()
    {
    }

    public static CondoUnitPrice Create(Guid condoUnitId)
    {
        return new CondoUnitPrice
        {
            Id = Guid.CreateVersion7(),
            CondoUnitId = condoUnitId,
            IsCorner = false,
            IsEdge = false,
            IsPoolView = false,
            IsSouth = false,
            IsOther = false
        };
    }

    public void UpdateLocationFlags(bool isCorner, bool isEdge, bool isPoolView, bool isSouth, bool isOther)
    {
        IsCorner = isCorner;
        IsEdge = isEdge;
        IsPoolView = isPoolView;
        IsSouth = isSouth;
        IsOther = isOther;
    }

    public void UpdateCalculatedValues(
        decimal? adjustPriceLocation,
        decimal? standardPrice,
        decimal? priceIncrementPerFloor,
        decimal? totalAppraisalValue,
        decimal? totalAppraisalValueRounded,
        decimal? forceSellingPrice,
        decimal? coverageAmount)
    {
        AdjustPriceLocation = adjustPriceLocation;
        StandardPrice = standardPrice;
        PriceIncrementPerFloor = priceIncrementPerFloor;
        TotalAppraisalValue = totalAppraisalValue;
        TotalAppraisalValueRounded = totalAppraisalValueRounded;
        ForceSellingPrice = forceSellingPrice;
        CoverageAmount = coverageAmount;
    }
}
