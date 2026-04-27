namespace Appraisal.Domain.Projects;

/// <summary>
/// Calculated pricing result for a project unit.
/// Covers both Condo (IsPoolView, IsSouth, PriceIncrementPerFloor) and
/// LandAndBuilding (IsNearGarden, LandIncreaseDecreaseAmount) shapes.
/// Type-specific flags should be left at default when not applicable.
/// </summary>
public class ProjectUnitPrice : Entity<Guid>
{
    public Guid ProjectUnitId { get; private set; }

    // ----- Location Flags -----

    // Common
    public bool IsCorner { get; private set; }
    public bool IsEdge { get; private set; }
    public bool IsOther { get; private set; }

    // Condo-only
    public bool IsPoolView { get; private set; }
    public bool IsSouth { get; private set; }

    // LB-only
    public bool IsNearGarden { get; private set; }

    // ----- Calculated Values -----
    public decimal? LandIncreaseDecreaseAmount { get; private set; } // LB only
    public decimal? AdjustPriceLocation { get; private set; }
    public decimal? StandardPrice { get; private set; }
    public decimal? PriceIncrementPerFloor { get; private set; }     // Condo only
    public decimal? TotalAppraisalValue { get; private set; }
    public decimal? TotalAppraisalValueRounded { get; private set; }
    public decimal? ForceSellingPrice { get; private set; }
    public decimal? CoverageAmount { get; private set; }

    private ProjectUnitPrice()
    {
    }

    public static ProjectUnitPrice Create(Guid projectUnitId)
    {
        return new ProjectUnitPrice
        {
            Id = Guid.CreateVersion7(),
            ProjectUnitId = projectUnitId,
            IsCorner = false,
            IsEdge = false,
            IsPoolView = false,
            IsSouth = false,
            IsNearGarden = false,
            IsOther = false
        };
    }

    /// <summary>Updates location flags for a Condo unit.</summary>
    public void UpdateCondoLocationFlags(bool isCorner, bool isEdge, bool isPoolView, bool isSouth, bool isOther)
    {
        IsCorner = isCorner;
        IsEdge = isEdge;
        IsPoolView = isPoolView;
        IsSouth = isSouth;
        IsOther = isOther;
        IsNearGarden = false;
    }

    /// <summary>Updates location flags for a LandAndBuilding unit.</summary>
    public void UpdateLandAndBuildingLocationFlags(bool isCorner, bool isEdge, bool isNearGarden, bool isOther)
    {
        IsCorner = isCorner;
        IsEdge = isEdge;
        IsNearGarden = isNearGarden;
        IsOther = isOther;
        IsPoolView = false;
        IsSouth = false;
    }

    public void UpdateCondoCalculatedValues(
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
        LandIncreaseDecreaseAmount = null;
    }

    public void UpdateLandAndBuildingCalculatedValues(
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
        PriceIncrementPerFloor = null;
    }
}
