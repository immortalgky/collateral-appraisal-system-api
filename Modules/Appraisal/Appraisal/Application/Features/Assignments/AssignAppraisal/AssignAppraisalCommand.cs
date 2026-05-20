namespace Appraisal.Application.Features.Assignments.AssignAppraisal;

/// <summary>
/// Relay command: forwards the admin's appraisal-assignment task input into the workflow.
/// Does NOT mutate AppraisalAssignment or create AppraisalFee rows — that's done downstream
/// by the workflow's CompanySelectionActivity / int-appraisal-execution publishers and their
/// respective integration-event handlers. Therefore no transactional marker — the workflow
/// service manages its own transaction internally.
/// </summary>
public record AssignAppraisalCommand(
    Guid AppraisalId,
    Guid WorkflowInstanceId,
    string? AssigneeUserId = null,
    string? AssigneeCompanyId = null,
    string? AssigneeCompanyName = null,
    string AssignmentMethod = "Manual",
    string? InternalAppraiserId = null,
    string? InternalFollowupAssignmentMethod = null,
    string AssignedBy = default,
    /// <summary>
    /// Workflow routing decision: "EXT" routes to company-selection (external company),
    /// "INT" routes to int-appraisal-execution (internal assignment).
    /// </summary>
    string DecisionTaken = "EXT"
) : ICommand<AssignAppraisalResult>;
