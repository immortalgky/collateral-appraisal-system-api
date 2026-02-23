using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.CreateVehicleProperty;

/// <summary>
/// Command to create a vehicle property with its appraisal detail
/// </summary>
public record CreateVehiclePropertyCommand(
    Guid AppraisalId,
    // Required
    string OwnerName,
    // Vehicle Identification
    string? PropertyName = null,
    string? VehicleName = null,
    string? EngineNo = null,
    string? ChassisNo = null,
    string? RegistrationNo = null,
    string? Description = null,
    // Vehicle Specifications
    string? Brand = null,
    string? Model = null,
    int? YearOfManufacture = null,
    string? CountryOfManufacture = null,
    // Purchase Info
    DateTime? PurchaseDate = null,
    decimal? PurchasePrice = null,
    // Dimensions
    string? Capacity = null,
    decimal? Width = null,
    decimal? Length = null,
    decimal? Height = null,
    // Energy
    string? EnergyUse = null,
    string? EnergyUseRemark = null,
    // Owner Details
    bool? IsOwnerVerified = null,
    // Usage & Condition
    bool? CanUse = null,
    string? Location = null,
    string? ConditionUse = null,
    string? VehicleCondition = null,
    int? VehicleAge = null,
    string? VehicleEfficiency = null,
    string? VehicleTechnology = null,
    string? UsePurpose = null,
    string? VehiclePart = null,
    // Appraiser Notes
    string? Remark = null,
    string? Other = null,
    string? AppraiserOpinion = null
) : ICommand<CreateVehiclePropertyResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
