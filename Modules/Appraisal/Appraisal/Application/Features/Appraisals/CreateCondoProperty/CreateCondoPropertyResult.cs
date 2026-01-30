namespace Appraisal.Application.Features.Appraisals.CreateCondoProperty;

/// <summary>
/// Result of creating a condo property
/// </summary>
public record CreateCondoPropertyResult(Guid PropertyId, Guid DetailId);
