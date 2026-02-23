namespace Appraisal.Application.Features.Appraisals.CreateLandProperty;

/// <summary>
/// Response for creating a land property
/// </summary>
public record CreateLandPropertyResponse(Guid PropertyId, Guid LandDetailId);
