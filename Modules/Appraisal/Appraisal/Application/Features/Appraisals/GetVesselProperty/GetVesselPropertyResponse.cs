namespace Appraisal.Application.Features.Appraisals.GetVesselProperty;

/// <summary>
/// Response for getting a vessel property
/// </summary>
public record GetVesselPropertyResponse(
    // Property
    Guid PropertyId,
    Guid AppraisalId,
    int SequenceNumber,
    string PropertyType,
    string? Description,
    // Detail
    Guid DetailId,
    // Vessel Identification
    string? PropertyName,
    string? VesselName,
    string? EngineNo,
    string? RegistrationNo,
    DateTime? RegistrationDate,
    // Vessel Specifications
    string? Brand,
    string? Model,
    int? YearOfManufacture,
    string? PlaceOfManufacture,
    string? VesselType,
    string? ClassOfVessel,
    // Purchase Info
    DateTime? PurchaseDate,
    decimal? PurchasePrice,
    // Dimensions
    string? EngineCapacity,
    decimal? Width,
    decimal? Length,
    decimal? Height,
    decimal? GrossTonnage,
    decimal? NetTonnage,
    // Energy
    string? EnergyUse,
    string? EnergyUseRemark,
    // Owner
    string Owner,
    bool VerifiableOwner,
    // Vessel Info
    bool CanUse,
    string? FormerName,
    string? VesselCurrentName,
    string? Location,
    // Condition & Assessment
    string? ConditionUse,
    string? VesselCondition,
    int? VesselAge,
    string? VesselEfficiency,
    string? VesselTechnology,
    string? UsePurpose,
    string? VesselPart,
    // Appraiser Notes
    string? Remark,
    string? Other,
    string? AppraiserOpinion
);
