namespace Appraisal.Application.Features.Appraisals.CreateVesselProperty;

/// <summary>
/// Result of creating a vessel property
/// </summary>
public record CreateVesselPropertyResult(Guid PropertyId, Guid DetailId);
