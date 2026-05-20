namespace Appraisal.Application.Features.Assignments.AssignAppraisal;

public record AssignAppraisalRequest(
    Guid WorkflowInstanceId,
    string AssignmentType,
    string? AssigneeUserId = null,
    string? AssigneeCompanyId = null,
    string? AssigneeCompanyName = null,
    string? AssignmentMethod = null,
    string? InternalAppraiserId = null,
    string? InternalFollowupAssignmentMethod = null,
    string? AssignedBy = null,
    /// <summary>
    /// Workflow routing decision: "EXT" for external company, "INT" for internal staff.
    /// Defaults to "EXT" when omitted.
    /// </summary>
    string DecisionTaken = "EXT");
