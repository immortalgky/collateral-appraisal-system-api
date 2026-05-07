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
    public decimal? FinalValueAdjusted { get; private set; }

    // Land Area Inclusion
    public bool IncludeLandArea { get; private set; } = true;
    public decimal? LandArea { get; private set; }
    public decimal? LandValue { get; private set; }   // user-edited land price

    // Building Cost (if applicable)
    public bool HasBuildingCost { get; private set; }
    public decimal? BuildingCost { get; private set; }
    public decimal? AppraisalPrice { get; private set; } // user-edited final total (HasBuildingCost only)

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
            Id = Guid.CreateVersion7(),
            PricingMethodId = pricingMethodId,
            FinalValue = finalValue,
            FinalValueRounded = finalValueRounded,
            IncludeLandArea = true,
            HasBuildingCost = false
        };
    }

    /// <summary>Deep-clone for CI carry-forward.</summary>
    public static PricingFinalValue CloneForMethod(PricingFinalValue source, Guid newMethodId)
    {
        return new PricingFinalValue
        {
            Id = Guid.CreateVersion7(),
            PricingMethodId = newMethodId,
            FinalValue = source.FinalValue,
            FinalValueRounded = source.FinalValueRounded,
            FinalValueAdjusted = source.FinalValueAdjusted,
            IncludeLandArea = source.IncludeLandArea,
            LandArea = source.LandArea,
            LandValue = source.LandValue,
            HasBuildingCost = source.HasBuildingCost,
            BuildingCost = source.BuildingCost,
            AppraisalPrice = source.AppraisalPrice
        };
    }

    public void SetLandAreaValues(decimal landArea, decimal landValue)
    {
        IncludeLandArea = true;
        LandArea = landArea;
        LandValue = landValue;
    }

    public void ExcludeLandArea()
    {
        IncludeLandArea = false;
        LandArea = null;
        LandValue = null;
    }

    public void SetBuildingCost(decimal buildingCost)
    {
        HasBuildingCost = true;
        BuildingCost = buildingCost;
    }

    public void UpdateFinalValue(decimal finalValue, decimal finalValueRounded)
    {
        FinalValue = finalValue;
        FinalValueRounded = finalValueRounded;
    }

    public void SetFinalValueAdjusted(decimal? value)
    {
        FinalValueAdjusted = value;
    }

    /// <summary>
    /// Persists the user-rounded appraisal price independent of the building-cost toggle.
    /// The same rounded number applies to land-only cost (unit 01/02), machinery (unit 03),
    /// market approach, and the with-building-cost case.
    /// </summary>
    public void SetAppraisalPrice(decimal? appraisalPrice)
    {
        AppraisalPrice = appraisalPrice;
    }

    public void ClearBuildingCost()
    {
        HasBuildingCost = false;
        BuildingCost = null;
    }

}
