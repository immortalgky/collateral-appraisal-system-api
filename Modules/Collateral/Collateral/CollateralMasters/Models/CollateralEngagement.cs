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

    /// <summary>
    /// Appraisal value AS IT STOOD at engagement time, with part-built buildings counted at their
    /// construction progress rather than at 100%:
    /// <c>land + buildings-with-no-inspection + inspected-buildings-at-current-progress</c>
    ///
    /// Computed by the Appraisal module's <c>IConstructionCurrentValueService</c> — the same code that
    /// builds the Decision Summary construction card — and frozen here so the outbound interfaces never
    /// recompute from later-overwritten master state.
    ///
    /// NULL when the appraisal had no construction inspection at all: nothing was part-built, so the
    /// current value is simply <see cref="AppraisalValue"/>. Read by the regulatory export's
    /// Appraisal-Value-as-Completed field, which falls back to the appraised value when this is NULL.
    /// </summary>
    public decimal? CurrentValue { get; private set; }

    /// <summary>
    /// Whether any building on this appraisal was still short of its finished value, frozen at
    /// engagement time. Read by the regulatory export (field 5) and the collateral catalog's
    /// under-construction filter.
    ///
    /// Previously lived on <c>LandDetails.IsUnderConstructionAtLastAppraisal</c>, which was a
    /// latest-wins cache on a mutable row: re-processing an older appraisal after a newer one
    /// overwrote it with the older state, silently. It also read a single property's inspection, so a
    /// multi-building appraisal reported whatever the primary property happened to say.
    ///
    /// NULL for engagements created before this column, and for appraisals with no inspection at all.
    /// </summary>
    public bool? IsUnderConstruction { get; private set; }

    /// <summary>
    /// Construction progress 0–100, weighted by value across every inspected building
    /// frozen at engagement time. Read off the percentages the inspector entered, not off a ratio of the money — see Appraisal.Domain.Appraisals.ConstructionMoney.
    /// Read by the regulatory export (field 6). NULL under the same conditions as
    /// <see cref="IsUnderConstruction"/>.
    /// </summary>
    public decimal? ConstructionProgressPercent { get; private set; }

    // NOTE: AS400 host state (HostCollateralId / redemption) is NOT here.
    //
    // It used to be, on the reasoning that the feed addresses rows by appraisal number and an
    // engagement is 1:1 with an appraisal. But that is only how the message is ADDRESSED — what it
    // describes is the collateral: AS400 mints one id per collateral at drawdown and reports
    // redemption against that same id, with no notion of which appraisal is involved. Holding it
    // here forced every reader to re-derive "which appraisal speaks for this collateral now", and
    // each did it slightly differently.
    //
    // → see CollateralMaster.HostCollateralId / IsRedeemed / RedeemedDate.

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
        string? appraisalCompanyCode = null,
        decimal? currentValue = null,
        bool? isUnderConstruction = null,
        decimal? constructionProgressPercent = null)
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
        CurrentValue = currentValue;
        IsUnderConstruction = isUnderConstruction;
        ConstructionProgressPercent = constructionProgressPercent;
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
