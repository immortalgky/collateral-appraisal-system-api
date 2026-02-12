namespace Appraisal.Application.Features.Appraisals.GetAppraisals;

/// <summary>
/// Filter request for GetAppraisals query
/// </summary>
public record GetAppraisalsFilterRequest(
    string? Status = null,
    string? Priority = null,
    string? AppraisalType = null,
    Guid? AssigneeUserId = null
);
