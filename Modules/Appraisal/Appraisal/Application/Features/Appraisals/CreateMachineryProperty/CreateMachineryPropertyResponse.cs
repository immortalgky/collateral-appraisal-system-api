namespace Appraisal.Application.Features.Appraisals.CreateMachineryProperty;

/// <summary>
/// Response returned after creating a machinery property
/// </summary>
public record CreateMachineryPropertyResponse(Guid PropertyId, Guid DetailId);
