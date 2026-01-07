namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Final values per pricing method.
/// </summary>
public class PricingFinalValue : Entity<Guid>
{
    public Guid PricingMethodId { get; private set; }

    // Final Value
    public decimal FinalValue { get; private set; }
    public decimal FinalValueRounded { get; private set; }

    // Land Area Inclusion
    public bool IncludeLandArea { get; private set; } = true;
    public decimal? LandArea { get; private set; }
    public decimal? AppraisalPrice { get; private set; }
    public decimal? AppraisalPriceRounded { get; private set; }
    public decimal? PriceDifferentiate { get; private set; }

    // Building Cost (if applicable)
    public bool HasBuildingCost { get; private set; }
    public decimal? BuildingCost { get; private set; }
    public decimal? AppraisalPriceWithBuilding { get; private set; }
    public decimal? AppraisalPriceWithBuildingRounded { get; private set; }

    private PricingFinalValue()
    {
    }

    public static PricingFinalValue Create(
        Guid pricingMethodId,
        decimal finalValue,
        decimal finalValueRounded)
    {
        return new PricingFinalValue
        {
            Id = Guid.NewGuid(),
            PricingMethodId = pricingMethodId,
            FinalValue = finalValue,
            FinalValueRounded = finalValueRounded,
            IncludeLandArea = true,
            HasBuildingCost = false
        };
    }

    public void SetLandAreaValues(decimal landArea, decimal appraisalPrice, decimal appraisalPriceRounded,
        decimal? priceDiff = null)
    {
        IncludeLandArea = true;
        LandArea = landArea;
        AppraisalPrice = appraisalPrice;
        AppraisalPriceRounded = appraisalPriceRounded;
        PriceDifferentiate = priceDiff;
    }

    public void ExcludeLandArea()
    {
        IncludeLandArea = false;
        LandArea = null;
        AppraisalPrice = null;
        AppraisalPriceRounded = null;
    }

    public void SetBuildingCost(decimal buildingCost, decimal priceWithBuilding, decimal priceWithBuildingRounded)
    {
        HasBuildingCost = true;
        BuildingCost = buildingCost;
        AppraisalPriceWithBuilding = priceWithBuilding;
        AppraisalPriceWithBuildingRounded = priceWithBuildingRounded;
    }

    public void UpdateFinalValue(decimal finalValue, decimal finalValueRounded)
    {
        FinalValue = finalValue;
        FinalValueRounded = finalValueRounded;
    }
}