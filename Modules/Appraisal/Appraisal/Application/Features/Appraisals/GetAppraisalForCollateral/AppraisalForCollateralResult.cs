namespace Appraisal.Application.Features.Appraisals.GetAppraisalForCollateral;

/// <summary>
/// Root DTO returned to the Collateral module.
/// Contains appraisal metadata + all in-scope properties (Land, LB, Condo, Leasehold, Machinery, Building).
/// Building properties are included so the consumer can sum building values onto the matching Land master
/// via BuildingAppraisalDetail.BuiltOnTitleNumber.
/// </summary>
public record AppraisalForCollateralResult(
    Guid AppraisalId,
    string? AppraisalNumber,
    string AppraisalType,
    DateTime? CompletedAt,
    Guid RequestId,
    string? RequestNumber,
    string? AppraiserUserId,
    string? CompanyId,
    string? CompanyName,
    // Appraisal-level total from ValuationAnalyses (Σ PricingAnalyses.FinalAppraisedValue across all PropertyGroups).
    decimal? AppraisedValue,
    IReadOnlyList<AppraisalPropertyForCollateral> Properties
);

/// <summary>
/// Per-property DTO covering all types.
/// Only the fields relevant to the property's type will be populated.
/// </summary>
public record AppraisalPropertyForCollateral(
    Guid PropertyId,
    string PropertyTypeCode,
    // --- Per-property final appraised value (from PricingAnalysis via PropertyGroup) ---
    decimal? AppraisedValue,
    // --- Land / LB fields ---
    LandIdentityForCollateral? LandIdentity,
    // --- Condo fields ---
    CondoIdentityForCollateral? CondoIdentity,
    // --- Leasehold fields ---
    LeaseholdIdentityForCollateral? LeaseholdIdentity,
    // --- Machinery fields ---
    MachineryIdentityForCollateral? MachineryIdentity,
    // --- Building fields (for BuiltOnTitleNumber matching) ---
    BuildingIdentityForCollateral? BuildingIdentity,
    // --- Construction inspection (Land / LB only) ---
    ConstructionInspectionForCollateral? ConstructionInspection
);

/// <summary>
/// Land-specific identity fields for collateral dedup.
/// LandOffice is a controlled-list dropdown value treated as LandOfficeCode.
/// TitleType on LandTitle is a controlled-list string value.
/// </summary>
public record LandIdentityForCollateral(
    // Available today — passed through directly
    string? Province,         // AdministrativeAddress.Province
    string? District,         // AdministrativeAddress.District (Amphur)
    string? SubDistrict,      // AdministrativeAddress.SubDistrict (Tambon)
    string? LandOffice,       // AdministrativeAddress.LandOffice (free-text — NOT a code)
    IReadOnlyList<LandTitleForCollateral> Titles
);

public record LandTitleForCollateral(
    Guid TitleId,
    string TitleNumber,       // LandTitle.TitleNumber
    string TitleType          // LandTitle.TitleType (free-text — NOT a canonical enum)
);

/// <summary>
/// Condo-specific identity fields for collateral dedup.
/// All required dedup fields are present: LandOffice (treated as LandOfficeCode),
/// CondoRegistrationNumber, BuildingNumber, FloorNumber, RoomNumber (UnitNumber),
/// TitleNumber and TitleType (unit deed identifiers).
/// </summary>
public record CondoIdentityForCollateral(
    string? CondoRegistrationNumber, // CondoAppraisalDetail.CondoRegistrationNumber
    string? BuildingNumber,          // CondoAppraisalDetail.BuildingNumber
    string? FloorNumber,             // CondoAppraisalDetail.FloorNumber
    string? RoomNumber,              // CondoAppraisalDetail.RoomNumber (= UnitNumber in spec)
    string? Province,                // CondoAppraisalDetail.Address?.Province
    string? LandOffice,              // CondoAppraisalDetail.Address?.LandOffice (= LandOfficeCode)
    string? TitleNumber,             // CondoAppraisalDetail.TitleNumber (unit deed number)
    string? TitleType                // CondoAppraisalDetail.TitleType (unit deed type)
);

/// <summary>
/// Leasehold-specific identity fields for collateral dedup.
/// ContractNo is used as the Tor Dor 11 lease registration number (dedup key).
/// UnderlyingMasterId is derived at upsert time by scanning sibling Land/LB properties.
/// </summary>
public record LeaseholdIdentityForCollateral(
    string? ContractNo,       // LeaseAgreementDetail.ContractNo (proxy for LeaseRegistrationNo)
    string? LessorName,       // LeaseAgreementDetail.LessorName
    string? LesseeName,       // LeaseAgreementDetail.LesseeName
    DateTime? LeaseStartDate, // LeaseAgreementDetail.LeaseStartDate
    DateTime? LeaseEndDate    // LeaseAgreementDetail.LeaseEndDate (last-known)
    // MISSING: LeaseRegistrationNo as a dedicated column
);

/// <summary>
/// Machinery-specific identity fields for collateral dedup.
/// Tier-1: RegistrationNo alone is sufficient.
/// Tier-2 (when RegistrationNo absent): SerialNo + Brand + Model + Manufacturer.
/// LocationOwner dropped from dedup key per v1 spec decision.
/// </summary>
public record MachineryIdentityForCollateral(
    string? RegistrationNo,   // MachineryAppraisalDetail.RegistrationNo (= MachineRegistrationNo in spec)
    string? SerialNo,         // MachineryAppraisalDetail.SerialNo (tier-2 dedup key)
    string? Brand,            // MachineryAppraisalDetail.Brand
    string? Model,            // MachineryAppraisalDetail.Model
    string? Manufacturer,     // MachineryAppraisalDetail.Manufacturer
    string? Location,         // MachineryAppraisalDetail.Location (informational, not dedup key)
    string? OwnerName         // MachineryAppraisalDetail.OwnerName (informational)
);

/// <summary>Building property — included solely for BuiltOnTitleNumber so the consumer
/// can sum building appraised values onto the matching Land master.</summary>
public record BuildingIdentityForCollateral(
    string? BuiltOnTitleNumber // BuildingAppraisalDetail.BuiltOnTitleNumber
);

/// <summary>
/// Construction inspection data for prefill and collateral master tracking.
/// </summary>
public record ConstructionInspectionForCollateral(
    Guid InspectionId,
    bool IsFullDetail,
    decimal OverallCurrentProgressPercent,
    // Full-detail mode
    IReadOnlyList<ConstructionWorkDetailForCollateral>? WorkDetails,
    // Summary mode
    decimal? SummaryCurrentProgressPct,
    decimal? SummaryCurrentValue,
    decimal? SummaryPreviousProgressPct,
    decimal? SummaryPreviousValue,
    string? SummaryDetail,
    string? Remark
);

/// <summary>
/// Per-work-item data for Progressive appraisal prefill.
/// The FE seeds a new inspection's PreviousProgressPct from CurrentProgressPct, matched by
/// ConstructionWorkItemId (template FK) or WorkItemName as fallback.
/// </summary>
public record ConstructionWorkDetailForCollateral(
    Guid WorkDetailId,
    Guid ConstructionWorkGroupId,
    Guid? ConstructionWorkItemId,
    string WorkItemName,
    int DisplayOrder,
    decimal ProportionPct,
    decimal PreviousProgressPct,
    decimal CurrentProgressPct,
    decimal CurrentProportionPct,
    decimal ConstructionValue
);
