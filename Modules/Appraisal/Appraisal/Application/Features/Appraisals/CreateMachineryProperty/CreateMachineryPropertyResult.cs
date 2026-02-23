namespace Appraisal.Application.Features.Appraisals.CreateMachineryProperty;

/// <summary>
/// Result of creating a machinery property
/// </summary>
public record CreateMachineryPropertyResult(Guid PropertyId, Guid DetailId);
