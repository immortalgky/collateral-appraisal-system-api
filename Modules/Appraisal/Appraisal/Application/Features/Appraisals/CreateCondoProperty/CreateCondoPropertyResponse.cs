namespace Appraisal.Application.Features.Appraisals.CreateCondoProperty;

/// <summary>
/// Response returned after creating a condo property
/// </summary>
public record CreateCondoPropertyResponse(Guid PropertyId, Guid DetailId);
