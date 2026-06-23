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
    string? Comment = null,
    string? AssignedBy = null,
    bool SubmitToWorkflow = true,
    /// <summary>
    /// Workflow routing decision: "EXT" for external company, "INT" for internal staff.
    /// Defaults to "EXT" when omitted.
    /// </summary>
    string DecisionTaken = "EXT");
