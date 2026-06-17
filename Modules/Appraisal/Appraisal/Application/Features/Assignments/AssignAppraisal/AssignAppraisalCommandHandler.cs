using Workflow;

namespace Appraisal.Application.Features.Assignments.AssignAppraisal;

/// <summary>
/// Thin relay handler: validates the pending assignment, then forwards the admin's
/// input into the workflow's "appraisal-assignment" task via <see cref="IWorkflowRelayService"/>.
///
/// The workflow engine routes on <c>decisionTaken</c>:
///   EXT → CompanySelectionActivity → CompanyAssignedIntegrationEvent →
///         CompanyAssignedIntegrationEventHandler (calls .Assign() + fee materialisation).
///   INT → int-appraisal-execution → WorkflowService.PublishInternalAssignedEvent →
///         InternalAssignedIntegrationEventHandler (calls .Assign() + tier-based fee).
///
/// This handler does NOT mutate AppraisalAssignment or create AppraisalFee rows directly.
/// </summary>
public class AssignAppraisalCommandHandler(
    IAppraisalRepository appraisalRepository,
    IWorkflowRelayService workflowRelayService)
    : ICommandHandler<AssignAppraisalCommand, AssignAppraisalResult>
{
    public async Task<AssignAppraisalResult> Handle(
        AssignAppraisalCommand command,
        CancellationToken cancellationToken)
    {
        // Validate that a pending assignment exists for this appraisal.
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        var pendingAssignment = appraisal.Assignments
            .FirstOrDefault(a => a.AssignmentStatus == AssignmentStatus.Pending)
            ?? throw new BadRequestException(
                $"No pending assignment found for appraisal '{command.AppraisalId}'. " +
                "The workflow task must be in Pending status to accept an assignment.");

        // Build input payload matching the keys the workflow's TaskActivity maps into variables.
        // The key names below MUST match the `inputMappings` declared on the appraisal-assignment
        // activity in appraisal-workflow.json — TaskActivity filters anything not declared there.
        // CompanySelectionActivity reads: assignedCompanyId / selectedCompanyId, assignedCompanyName /
        // selectedCompanyName, assignmentMethod. The RoutingActivity reads: decisionTaken.
        var input = new Dictionary<string, object>
        {
            ["selectedCompanyId"] = command.AssigneeCompanyId ?? string.Empty,
            ["selectedCompanyName"] = command.AssigneeCompanyName ?? string.Empty,
            ["assignmentMethod"] = command.AssignmentMethod,
            ["decisionTaken"] = command.DecisionTaken,
            ["internalFollowupStaffId"] = command.InternalAppraiserId ?? string.Empty,
            ["internalFollowupMethod"] = command.InternalFollowupAssignmentMethod ?? string.Empty
        };

        // INT path: pin the admin-selected internal appraiser (the int-appraisal-execution
        // EXECUTOR, carried in AssigneeUserId) onto int-appraisal-execution; otherwise the
        // activity's default round-robin strategy picks a different user. Note: InternalAppraiserId
        // is the separate followup/checker field and feeds internalFollowupStaffId above — it is
        // NOT the executor.
        IReadOnlyDictionary<string, WorkflowAssigneeOverride>? overrides = null;
        if (string.Equals(command.DecisionTaken, "INT", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(command.AssigneeUserId))
        {
            overrides = new Dictionary<string, WorkflowAssigneeOverride>
            {
                ["int-appraisal-execution"] = new WorkflowAssigneeOverride(
                    Assignee: command.AssigneeUserId,
                    Reason: "Admin-selected internal appraiser",
                    OverrideBy: command.AssignedBy)
            };
        }

        await workflowRelayService.ResumeWorkflowAsync(
            command.WorkflowInstanceId,
            "appraisal-assignment",
            command.AssignedBy,
            input,
            overrides,
            cancellationToken);

        return new AssignAppraisalResult(pendingAssignment.Id);
    }
}
