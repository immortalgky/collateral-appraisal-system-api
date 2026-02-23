namespace Appraisal.Application.Features.Appraisals.UpdatePropertyGroup;

public record UpdatePropertyGroupRequest(
    string GroupName,
    string? Description,
    bool UseSystemCalc
);
