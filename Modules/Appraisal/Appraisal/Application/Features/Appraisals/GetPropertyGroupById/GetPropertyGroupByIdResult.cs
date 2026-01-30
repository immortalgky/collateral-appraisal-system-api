namespace Appraisal.Application.Features.Appraisals.GetPropertyGroupById;

/// <summary>
/// Result of getting a property group by ID
/// </summary>
public record GetPropertyGroupByIdResult(
    Guid Id,
    int GroupNumber,
    string GroupName,
    string? Description,
    bool UseSystemCalc,
    List<PropertyGroupItemDto> Properties
);
