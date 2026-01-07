using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UpdateBuildingProperty;

/// <summary>
/// Command to update a building property detail
/// </summary>
public record UpdateBuildingPropertyCommand(
    Guid AppraisalId,
    Guid PropertyId,
    // Property Identification
    string? PropertyName = null,
    string? BuildingNumber = null,
    string? ModelName = null,
    string? BuiltOnTitleNumber = null,
    // Owner
    string? OwnerName = null,
    bool? IsOwnerVerified = null,
    string? HouseNumber = null,
    // Building Status
    string? BuildingCondition = null,
    bool? IsUnderConstruction = null,
    decimal? ConstructionCompletionPercent = null,
    DateTime? ConstructionLicenseExpirationDate = null,
    bool? IsAppraisable = null,
    bool? HasObligation = null,
    string? ObligationDetails = null,
    // Building Info
    string? BuildingType = null,
    string? BuildingTypeOther = null,
    int? NumberOfFloors = null,
    string? DecorationType = null,
    string? DecorationTypeOther = null,
    bool? IsEncroached = null,
    string? EncroachmentRemark = null,
    decimal? EncroachmentArea = null,
    // Construction Details
    string? BuildingMaterial = null,
    string? BuildingStyle = null,
    bool? IsResidential = null,
    int? BuildingAge = null,
    int? ConstructionYear = null,
    string? IsResidentialRemark = null,
    string? ConstructionStyleType = null,
    string? ConstructionStyleRemark = null,
    // Structure Components
    string? StructureType = null,
    string? StructureTypeOther = null,
    string? RoofFrameType = null,
    string? RoofFrameTypeOther = null,
    string? RoofType = null,
    string? RoofTypeOther = null,
    string? CeilingType = null,
    string? CeilingTypeOther = null,
    string? InteriorWallType = null,
    string? InteriorWallTypeOther = null,
    string? ExteriorWallType = null,
    string? ExteriorWallTypeOther = null,
    string? FenceType = null,
    string? FenceTypeOther = null,
    string? ConstructionType = null,
    string? ConstructionTypeOther = null,
    // Utilization
    string? UtilizationType = null,
    string? OtherPurposeUsage = null,
    // Area & Pricing
    decimal? TotalBuildingArea = null,
    decimal? BuildingInsurancePrice = null,
    decimal? SellingPrice = null,
    decimal? ForcedSalePrice = null,
    // Other
    string? Remark = null
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
