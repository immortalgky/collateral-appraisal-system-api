namespace Collateral.CollateralMasters.Models;

/// <summary>
/// One row per Building property captured at engagement time.
/// Zero rows for bare L / LSL engagements; one or more rows for LB / LB-type engagements.
/// Written once when the engagement is created; never mutated.
/// </summary>
public class CollateralEngagementBuilding
{
    public Guid Id { get; private set; }
    public Guid EngagementId { get; private set; }

    /// <summary>
    /// Parameter code from parameter group "BuildingType" (e.g. "01" = House, "02" = Commercial).
    /// Sourced from BuildingAppraisalDetail.BuildingType.
    /// </summary>
    public string BuildingTypeCode { get; private set; } = null!;

    /// <summary>
    /// Total building area in sq.m at engagement time.
    /// Sourced from BuildingAppraisalDetail.TotalBuildingArea.
    /// </summary>
    public decimal? BuildingArea { get; private set; }

    /// <summary>
    /// Building appraised value at engagement time (from pricing analysis).
    /// Sourced from the building property's PricingInfo.AppraisalValue if available.
    /// </summary>
    public decimal? BuildingValue { get; private set; }

    /// <summary>Display order within the engagement (1..N).</summary>
    public int Sequence { get; private set; }

    private CollateralEngagementBuilding() { }

    internal static CollateralEngagementBuilding Create(
        Guid engagementId,
        string buildingTypeCode,
        decimal? buildingArea,
        decimal? buildingValue,
        int sequence)
    {
        return new CollateralEngagementBuilding
        {
            Id = Guid.CreateVersion7(),
            EngagementId = engagementId,
            BuildingTypeCode = buildingTypeCode,
            BuildingArea = buildingArea,
            BuildingValue = buildingValue,
            Sequence = sequence,
        };
    }
}
