namespace Appraisal.Application.Features.Appraisals.CreateVehicleProperty;

/// <summary>
/// Result of creating a vehicle property
/// </summary>
public record CreateVehiclePropertyResult(Guid PropertyId, Guid DetailId);
