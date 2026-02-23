namespace Appraisal.Application.Features.Appraisals.GetVehicleProperty;

/// <summary>
/// Response for getting a vehicle property
/// </summary>
public record GetVehiclePropertyResponse(
    // Property
    Guid PropertyId,
    Guid AppraisalId,
    int SequenceNumber,
    string PropertyType,
    string? Description,
    // Detail
    Guid DetailId,
    // Vehicle Identification
    string? PropertyName,
    string? VehicleName,
    string? EngineNo,
    string? ChassisNo,
    string? RegistrationNo,
    // Vehicle Specifications
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
    string? VehicleCondition,
    int? VehicleAge,
    string? VehicleEfficiency,
    string? VehicleTechnology,
    string? UsePurpose,
    string? VehiclePart,
    // Appraiser Notes
    string? Remark,
    string? Other,
    string? AppraiserOpinion
);
