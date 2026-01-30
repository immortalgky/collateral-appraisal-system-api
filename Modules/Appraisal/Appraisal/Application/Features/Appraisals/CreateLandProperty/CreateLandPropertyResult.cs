namespace Appraisal.Application.Features.Appraisals.CreateLandProperty;

/// <summary>
/// Result of creating a land property
/// </summary>
public record CreateLandPropertyResult(Guid PropertyId, Guid LandDetailId);
