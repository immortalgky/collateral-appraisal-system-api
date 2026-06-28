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
    /// <summary>
    /// HostCompanyCode from auth.Companies at engagement time (historically frozen).
    /// Used by the outbound Collateral Result interface (External Valuer Code, CCDAPC, 4-char).
    /// NULL for engagements that pre-date this column, or when the assignment is internal.
    /// </summary>
    public string? AppraisalCompanyCode { get; private set; }
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

    /// <summary>
    /// Appraisal-level forced-sale value at engagement time (ValuationAnalyses.ForcedSaleValue).
    /// Used by the outbound Collateral Result interface (Force Sale Value). NULL when not present.
    /// </summary>
    public decimal? ForcedSaleValue { get; private set; }

    /// <summary>
    /// Bank-side internal valuer display name at engagement time
    /// (AppraisalAssignment.InternalAppraiserName). Used by the outbound Collateral Result interface
    /// (Internal Valuer Name). NULL when not captured.
    /// </summary>
    public string? InternalAppraiserName { get; private set; }

    /// <summary>
    /// Cost-approach land value at engagement time (UnitPrice × land area), frozen here so the
    /// outbound Collateral Result interface doesn't recompute from later-overwritten master state.
    /// NULL for non-Land/L&B or non-cost-approach.
    /// </summary>
    public decimal? LandValue { get; private set; }

    /// <summary>
    /// Cost-approach building value at engagement time (PricingFinalValue.BuildingValue), frozen.
    /// NULL for non-L&B or non-cost-approach.
    /// </summary>
    public decimal? BuildingValue { get; private set; }

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
        decimal? appraisalValue = null,
        decimal? forcedSaleValue = null,
        string? internalAppraiserName = null,
        decimal? landValue = null,
        decimal? buildingValue = null,
        string? appraisalCompanyCode = null)
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
        ForcedSaleValue = forcedSaleValue;
        InternalAppraiserName = internalAppraiserName;
        LandValue = landValue;
        BuildingValue = buildingValue;
        AppraisalCompanyCode = appraisalCompanyCode;
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
        int sequence,
        int? buildingAge,
        decimal? numberOfFloors)
    {
        var building = CollateralEngagementBuilding.Create(
            Id, buildingTypeCode, buildingArea, buildingValue, sequence, buildingAge, numberOfFloors);
        _buildings.Add(building);
    }
}
