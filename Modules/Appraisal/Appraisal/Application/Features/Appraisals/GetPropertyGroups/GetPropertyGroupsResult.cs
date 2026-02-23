namespace Appraisal.Application.Features.Appraisals.GetPropertyGroups;

/// <summary>
/// Result of getting property groups
/// </summary>
public record GetPropertyGroupsResult(
    List<PropertyGroupDto> Groups
);
