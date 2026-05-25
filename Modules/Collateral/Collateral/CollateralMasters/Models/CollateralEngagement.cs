namespace Collateral.CollateralMasters.Models;

public class CollateralEngagement
{
    public Guid Id { get; private set; }
    public Guid CollateralMasterId { get; private set; }
    public Guid AppraisalId { get; private set; }
    public string AppraisalNumber { get; private set; } = null!;
    public Guid RequestId { get; private set; }
    public string RequestNumber { get; private set; } = null!;
    // PropertyId dropped (PR-4): engagement is now per-appraisal, not per-property.
    // Members live inside the Snapshot's groups[*].properties[] array.
    public string AppraisalType { get; private set; } = null!;
    public DateTime AppraisalDate { get; private set; }
    // AppraisedValue dropped (PR-4): group-level values live on master detail rows
    // (LandDetail.AppraisalValue etc.) and inside the engagement Snapshot JSON.
    public string? AppraiserUserId { get; private set; }
    public Guid? AppraisalCompanyId { get; private set; }
    public string? AppraisalCompanyName { get; private set; }
    // Construction Inspection Fee captured from this engagement's AppraisalFee.
    // Reused as the appraisal fee when a future Construction Inspection appraisal is created
    // for the same collateral (CI bypasses the normal tier/quotation pipeline).
    public decimal? ConstructionInspectionFeeAmount { get; private set; }
    public string Snapshot { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    // --- Engagement-time history fields (written once at creation, never updated) ---

    /// <summary>
    /// CollateralType code at the time of this appraisal engagement (historically frozen).
    /// Sourced from property.PropertyTypeCode at upsert time.
    /// NULL for engagements that pre-date this column.
    /// </summary>
    public string? AppraisedCollateralType { get; private set; }

    /// <summary>
    /// Land area in sq.wa at engagement time. NULL for non-Land types.
    /// Sourced from LandIdentity.LandArea (= LandAppraisalDetail.TotalLandAreaInSqWa).
    /// </summary>
    public decimal? LandAreaInSqWa { get; private set; }

    /// <summary>
    /// Group-level appraisal value at engagement time (historically frozen).
    /// Sourced from PricingInfo.AppraisalValue (the group-shared value from PricingFinalValue).
    /// NULL for engagements that pre-date this column, or when no pricing analysis exists.
    /// </summary>
    public decimal? AppraisalValue { get; private set; }

    // Buildings child collection — one row per Building property at engagement time.
    private readonly List<CollateralEngagementBuilding> _buildings = [];
    public IReadOnlyList<CollateralEngagementBuilding> Buildings => _buildings.AsReadOnly();

    private CollateralEngagement() { }

    internal CollateralEngagement(
        Guid collateralMasterId,
        Guid appraisalId,
        string appraisalNumber,
        Guid requestId,
        string requestNumber,
        string appraisalType,
        DateTime appraisalDate,
        string? appraiserUserId,
        Guid? appraisalCompanyId,
        string? appraisalCompanyName,
        decimal? constructionInspectionFeeAmount,
        string snapshot,
        DateTime createdAt,
        string? appraisedCollateralType = null,
        decimal? landAreaInSqWa = null,
        decimal? appraisalValue = null)
    {
        Id = Guid.CreateVersion7();
        CollateralMasterId = collateralMasterId;
        AppraisalId = appraisalId;
        AppraisalNumber = appraisalNumber;
        RequestId = requestId;
        RequestNumber = requestNumber;
        AppraisalType = appraisalType;
        AppraisalDate = appraisalDate;
        AppraiserUserId = appraiserUserId;
        AppraisalCompanyId = appraisalCompanyId;
        AppraisalCompanyName = appraisalCompanyName;
        ConstructionInspectionFeeAmount = constructionInspectionFeeAmount;
        Snapshot = snapshot;
        CreatedAt = createdAt;
        AppraisedCollateralType = appraisedCollateralType;
        LandAreaInSqWa = landAreaInSqWa;
        AppraisalValue = appraisalValue;
    }

    /// <summary>
    /// Appends a building to this engagement's building list.
    /// Called by the upsert service for each Building property whose BuiltOnTitleNumber
    /// matches one of the titles in this engagement's land group.
    /// </summary>
    internal void AddBuilding(
        string buildingTypeCode,
        decimal? buildingArea,
        decimal? buildingValue,
        int sequence)
    {
        var building = CollateralEngagementBuilding.Create(
            Id, buildingTypeCode, buildingArea, buildingValue, sequence);
        _buildings.Add(building);
    }
}
