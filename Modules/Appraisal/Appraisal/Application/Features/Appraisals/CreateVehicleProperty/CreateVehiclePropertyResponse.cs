namespace Appraisal.Application.Features.Appraisals.CreateVehicleProperty;

/// <summary>
/// Response returned after creating a vehicle property
/// </summary>
public record CreateVehiclePropertyResponse(Guid PropertyId, Guid DetailId);
