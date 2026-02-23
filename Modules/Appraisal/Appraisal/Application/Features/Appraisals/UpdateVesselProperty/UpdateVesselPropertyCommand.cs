using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UpdateVesselProperty;

/// <summary>
/// Command to update a vessel property detail
/// </summary>
public record UpdateVesselPropertyCommand(
    Guid AppraisalId,
    Guid PropertyId,
    // Vessel Identification
    string? PropertyName = null,
    string? VesselName = null,
    string? EngineNo = null,
    string? RegistrationNo = null,
    DateTime? RegistrationDate = null,
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
    // Owner
    string? OwnerName = null,
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
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
