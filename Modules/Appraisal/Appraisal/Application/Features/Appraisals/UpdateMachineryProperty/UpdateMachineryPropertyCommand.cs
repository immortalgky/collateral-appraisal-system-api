using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UpdateMachineryProperty;

/// <summary>
/// Command to update a machinery property detail
/// </summary>
public record UpdateMachineryPropertyCommand(
    Guid AppraisalId,
    Guid PropertyId,
    // Machine Identification
    string? PropertyName = null,
    string? MachineName = null,
    string? EngineNo = null,
    string? ChassisNo = null,
    string? RegistrationNo = null,
    // Machine Specifications
    string? Brand = null,
    string? Model = null,
    string? Series = null,
    int? YearOfManufacture = null,
    string? Manufacturer = null,
    // Purchase Info
    DateTime? PurchaseDate = null,
    decimal? PurchasePrice = null,
    // Dimensions
    string? Capacity = null,
    int? Quantity = null,
    string? MachineDimensions = null,
    decimal? Width = null,
    decimal? Length = null,
    decimal? Height = null,
    // Energy
    string? EnergyUse = null,
    string? EnergyUseRemark = null,
    // Owner
    string? OwnerName = null,
    bool? IsOwnerVerified = null,
    // Usage & Condition
    bool? IsOperational = null,
    string? Location = null,
    string? ConditionUse = null,
    string? MachineCondition = null,
    int? MachineAge = null,
    string? MachineEfficiency = null,
    string? MachineTechnology = null,
    string? UsagePurpose = null,
    string? MachineParts = null,
    // Valuation
    decimal? ReplacementValue = null,
    decimal? ConditionValue = null,
    // Appraiser Notes
    string? Remark = null,
    string? Other = null,
    string? AppraiserOpinion = null
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;