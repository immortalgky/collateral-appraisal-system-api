namespace Appraisal.Application.Features.Appraisals.GetMachineryProperty;

/// <summary>
/// Response for getting a machinery property
/// </summary>
public record GetMachineryPropertyResponse(
    // Property
    Guid PropertyId,
    Guid AppraisalId,
    int SequenceNumber,
    string PropertyType,
    string? Description,
    // Detail
    Guid DetailId,
    // Machine Identification
    string? PropertyName,
    string? MachineName,
    string? EngineNo,
    string? ChassisNo,
    string? RegistrationNo,
    // Machine Specifications
    string? Brand,
    string? Model,
    string? Series,
    int? YearOfManufacture,
    string? Manufacturer,
    // Purchase Info
    DateTime? PurchaseDate,
    decimal? PurchasePrice,
    // Dimensions
    string? Capacity,
    int? Quantity,
    string? MachineDimensions,
    decimal? Width,
    decimal? Length,
    decimal? Height,
    // Energy
    string? EnergyUse,
    string? EnergyUseRemark,
    // Owner
    string? OwnerName,
    bool VerifiableOwner,
    // Usage & Condition
    bool IsOperational,
    string? Location,
    string? ConditionUse,
    string? MachineCondition,
    int? MachineAge,
    string? MachineEfficiency,
    string? MachineTechnology,
    string? UsagePurpose,
    string? MachineParts,
    // Valuation
    decimal? ReplacementValue,
    decimal? ConditionValue,
    // Appraiser Notes
    string? Remark,
    string? Other,
    string? AppraiserOpinion
);