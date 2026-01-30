namespace Appraisal.Application.Features.Appraisals.CreateBuildingProperty;

/// <summary>
/// Response returned after creating a building property
/// </summary>
public record CreateBuildingPropertyResponse(Guid PropertyId, Guid DetailId);
