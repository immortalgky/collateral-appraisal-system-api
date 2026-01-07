using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.CreateVesselProperty;

/// <summary>
/// Command to create a vessel property with its appraisal detail
/// </summary>
public record CreateVesselPropertyCommand(
    Guid AppraisalId,
    // Required
    string OwnerName,
    // Vessel Identification
    string? PropertyName = null,
    string? VesselName = null,
    string? EngineNo = null,
    string? RegistrationNo = null,
    DateTime? RegistrationDate = null,
    string? Description = null,
    // Vessel Specifications
    string? Brand = null,
    string? Model = null,
    int? YearOfManufacture = null,
    string? PlaceOfManufacture = null,
    string? VesselType = null,
    string? ClassOfVessel = null,
    // Purchase Info
    DateTime? PurchaseDate = null,
    decimal? PurchasePrice = null,
    // Dimensions
    string? EngineCapacity = null,
    decimal? Width = null,
    decimal? Length = null,
    decimal? Height = null,
    decimal? GrossTonnage = null,
    decimal? NetTonnage = null,
    // Energy
    string? EnergyUse = null,
    string? EnergyUseRemark = null,
    // Owner Details
    bool? IsOwnerVerified = null,
    // Vessel Info
    bool? CanUse = null,
    string? FormerName = null,
    string? VesselCurrentName = null,
    string? Location = null,
    // Condition & Assessment
    string? ConditionUse = null,
    string? VesselCondition = null,
    int? VesselAge = null,
    string? VesselEfficiency = null,
    string? VesselTechnology = null,
    string? UsePurpose = null,
    string? VesselPart = null,
    // Appraiser Notes
    string? Remark = null,
    string? Other = null,
    string? AppraiserOpinion = null
) : ICommand<CreateVesselPropertyResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
