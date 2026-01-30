namespace Appraisal.Application.Features.Appraisals.CreateVesselProperty;

/// <summary>
/// Response returned after creating a vessel property
/// </summary>
public record CreateVesselPropertyResponse(Guid PropertyId, Guid DetailId);
