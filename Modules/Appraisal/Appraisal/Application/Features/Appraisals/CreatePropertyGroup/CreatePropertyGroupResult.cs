namespace Appraisal.Application.Features.Appraisals.CreatePropertyGroup;

/// <summary>
/// Result of creating a PropertyGroup
/// </summary>
public record CreatePropertyGroupResult(
    Guid Id,
    int GroupNumber
);
