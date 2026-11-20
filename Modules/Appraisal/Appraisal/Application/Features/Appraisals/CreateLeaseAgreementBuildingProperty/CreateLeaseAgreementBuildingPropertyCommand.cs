using Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;
using Appraisal.Application.Features.Appraisals.Shared;

namespace Appraisal.Application.Features.Appraisals.CreateLeaseAgreementBuildingProperty;

/// <summary>
/// Command to create a lease agreement building property with its appraisal detail
/// </summary>
public record CreateLeaseAgreementBuildingPropertyCommand(
    Guid AppraisalId,
    Guid? GroupId = null,
    // Property Identification
    string? PropertyName = null,
    string? BuildingNumber = null,
    string? ModelName = null,
    string? BuiltOnTitleNumber = null,
    // Owner
    string? OwnerNameBuilding = null,
    bool? IsOwnerVerifiedBuilding = null,
    string? HouseNumber = null,
    string? HouseNumberStatus = null,
    // Building Status
    string? BuildingConditionType = null,
    string? BuildingConditionTypeOther = null,
    bool? IsUnderConstruction = null,
    DateTime? ConstructionLicenseExpirationDate = null,
    string? ConstructionLicenseNumber = null,
    bool? IsAppraisable = null,
    string? NotAppraisableReason = null,
    List<string>? ObligationType = null,
    string? ObligationDetails = null,
    string? MortgageDetails = null,
    // Building Info
    string? BuildingType = null,
    string? BuildingTypeOther = null,
    decimal? NumberOfFloors = null,
    string? DecorationType = null,
    string? DecorationTypeOther = null,
    bool? IsEncroachingOthers = null,
    string? EncroachingOthersRemark = null,
    decimal? EncroachingOthersArea = null,
    // Construction Details
    string? BuildingMaterialType = null,
    string? BuildingStyleType = null,
    string? BuildingStyleTypeOther = null,
    bool? IsResidential = null,
    int? BuildingAge = null,
    int? ConstructionYear = null,
    string? ResidentialRemark = null,
    string? ConstructionStyleType = null,
    string? ConstructionStyleRemark = null,
    // Structure Components
    List<string>? StructureType = null,
    string? StructureTypeOther = null,
    List<string>? RoofFrameType = null,
    string? RoofFrameTypeOther = null,
    List<string>? RoofType = null,
    string? RoofTypeOther = null,
    List<string>? CeilingType = null,
    string? CeilingTypeOther = null,
    List<string>? InteriorWallType = null,
    string? InteriorWallTypeOther = null,
    List<string>? ExteriorWallType = null,
    string? ExteriorWallTypeOther = null,
    List<string>? FenceType = null,
    string? FenceTypeOther = null,
    string? ConstructionType = null,
    string? ConstructionTypeOther = null,
    // Building detail (appraisal book)
    decimal? BuildingWidth = null,
    decimal? BuildingLength = null,
    string? SpaceDivision = null,
    string? MaintenanceType = null,
    string? MaintenanceTypeOther = null,
    List<string>? DoorType = null,
    string? DoorTypeOther = null,
    List<string>? WindowType = null,
    string? WindowTypeOther = null,
    List<string>? SanitaryType = null,
    string? SanitaryTypeOther = null,
    List<string>? BuildingSystemType = null,
    string? BuildingSystemTypeOther = null,
    // Utilization
    string? UtilizationType = null,
    string? UtilizationTypeOther = null,
    // Area & Pricing
    decimal? TotalBuildingArea = null,
    decimal? BuildingCostValue = null,
    decimal? BuildingInsurancePrice = null,
    // Other
    string? Remark = null,
    // Depreciation Details
    List<DepreciationItemData>? DepreciationDetails = null,
    // Surfaces
    List<SurfaceItemData>? Surfaces = null,
    // Construction Inspection (null = skip)
    ConstructionInspectionData? ConstructionInspection = null,
    // Lease Agreement & Rental Info
    LeaseAgreementData? LeaseAgreement = null,
    RentalInfoData? RentalInfo = null
) : ICommand<CreateLeaseAgreementBuildingPropertyResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
