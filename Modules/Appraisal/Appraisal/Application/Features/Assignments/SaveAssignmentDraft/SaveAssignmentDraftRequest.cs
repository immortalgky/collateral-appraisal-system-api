namespace Appraisal.Application.Features.Assignments.SaveAssignmentDraft;

public record SaveAssignmentDraftRequest(
    string AssignmentType,
    string? AssigneeUserId = null,
    string? AssigneeCompanyId = null,
    string? AssignmentMethod = null,
    string? InternalAppraiserId = null,
    string? InternalFollowupAssignmentMethod = null,
    string? Remark = null);
