namespace Appraisal.Application.Features.Appraisals.GetAppraisalViews;

public record GetAppraisalViewsResult(IReadOnlyList<SmartViewDto> Views);

public record SmartViewDto(
    string Key,
    string Name,
    string Description,
    Dictionary<string, string> Filters
);
