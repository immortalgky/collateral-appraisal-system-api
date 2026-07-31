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
    /// Workflow routing decision: "EXT" for external company, "INT" for internal staff,
    /// "EXTO" for a company engaged outside the system whose book is keyed in internally.
    /// Defaults to "EXT" when omitted.
    /// </summary>
    string DecisionTaken = "EXT",
    /// <summary>Admin remark persisted onto the assignment row alongside the assign action.</summary>
    string? Remark = null);
