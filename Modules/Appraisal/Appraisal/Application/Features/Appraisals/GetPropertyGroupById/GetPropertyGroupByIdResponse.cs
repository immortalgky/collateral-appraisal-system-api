namespace Appraisal.Application.Features.Appraisals.GetPropertyGroupById;

public record GetPropertyGroupByIdResponse(
    Guid Id,
    int GroupNumber,
    string GroupName,
    string? Description,
    bool UseSystemCalc,
    List<PropertyGroupItemDto> Properties
);
