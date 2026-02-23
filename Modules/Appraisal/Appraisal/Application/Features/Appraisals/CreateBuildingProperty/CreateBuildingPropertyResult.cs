namespace Appraisal.Application.Features.Appraisals.CreateBuildingProperty;

/// <summary>
/// Result of creating a building property
/// </summary>
public record CreateBuildingPropertyResult(Guid PropertyId, Guid DetailId);
