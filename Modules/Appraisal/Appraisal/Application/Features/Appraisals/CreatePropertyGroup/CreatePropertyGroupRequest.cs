namespace Appraisal.Application.Features.Appraisals.CreatePropertyGroup;

public record CreatePropertyGroupRequest(
    string GroupName,
    string? Description = null
);
