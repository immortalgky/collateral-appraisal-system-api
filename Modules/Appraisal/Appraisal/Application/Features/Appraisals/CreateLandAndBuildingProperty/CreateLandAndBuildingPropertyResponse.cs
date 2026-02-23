namespace Appraisal.Application.Features.Appraisals.CreateLandAndBuildingProperty;

/// <summary>
/// Response returned after creating a land and building property
/// </summary>
public record CreateLandAndBuildingPropertyResponse(Guid PropertyId, Guid DetailId);
