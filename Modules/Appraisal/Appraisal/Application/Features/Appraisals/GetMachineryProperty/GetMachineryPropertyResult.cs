namespace Appraisal.Application.Features.Appraisals.GetMachineryProperty;

/// <summary>
/// Result of getting a machinery property
/// </summary>
public record GetMachineryPropertyResult(
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
    int? YearOfManufacture,
    string? CountryOfManufacture,
    // Purchase Info
    DateTime? PurchaseDate,
    decimal? PurchasePrice,
    // Dimensions
    string? Capacity,
    decimal? Width,
    decimal? Length,
    decimal? Height,
    // Energy
    string? EnergyUse,
    string? EnergyUseRemark,
    // Owner
    string Owner,
    bool VerifiableOwner,
    // Usage & Condition
    bool CanUse,
    string? Location,
    string? ConditionUse,
    string? MachineCondition,
    int? MachineAge,
    string? MachineEfficiency,
    string? MachineTechnology,
    string? UsePurpose,
    string? MachinePart,
    // Appraiser Notes
    string? Remark,
    string? Other,
    string? AppraiserOpinion
);
