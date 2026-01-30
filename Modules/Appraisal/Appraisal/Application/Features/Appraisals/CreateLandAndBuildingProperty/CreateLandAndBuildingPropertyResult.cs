namespace Appraisal.Application.Features.Appraisals.CreateLandAndBuildingProperty;

/// <summary>
/// Result of creating a land and building property
/// </summary>
public record CreateLandAndBuildingPropertyResult(Guid PropertyId, Guid DetailId);
