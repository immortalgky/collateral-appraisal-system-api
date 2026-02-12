namespace Appraisal.Application.Features.Assignments.AssignAppraisal;

public record AssignAppraisalRequest(
    string AssignmentMode,
    Guid? AssigneeUserId = null,
    Guid? AssigneeCompanyId = null,
    string? AssignmentSource = null,
    Guid? AssignedBy = null);
