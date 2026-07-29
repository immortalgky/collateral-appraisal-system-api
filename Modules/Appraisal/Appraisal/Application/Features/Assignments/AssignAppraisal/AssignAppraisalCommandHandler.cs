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
    IAppraisalUnitOfWork unitOfWork,
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

        // Persist the admin remark onto the row BEFORE resuming the workflow. Committing here (rather
        // than relying on a transactional marker) guarantees the value is durable before the relay
        // triggers the downstream CompanyAssigned/InternalAssigned handlers — .Assign() there does not
        // touch Remark, so it survives. This handler otherwise stays a pure relay (no transactional marker).
        if (command.Remark is not null)
        {
            pendingAssignment.SetRemark(command.Remark);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Build input payload matching the keys the workflow's TaskActivity maps into variables.
        // The key names below MUST match the `inputMappings` declared on the appraisal-assignment
        // activity in appraisal-workflow.json — TaskActivity filters anything not declared there.
        // CompanySelectionActivity reads: assignedCompanyId / selectedCompanyId, assignedCompanyName /
        // selectedCompanyName, assignmentMethod. The RoutingActivity reads: decisionTaken.
        // EXTO path: the bank engaged an external company OUTSIDE the system and an internal
        // appraiser keys the resulting book in. The case must count as External everywhere
        // downstream (report rendering, AS400/LOS feed, fee) even though no ext-* activity ever
        // runs, so stamp assignmentType here rather than letting int-* defaults claim it.
        // isOfflineExternal is what the route-back transitions use to come back to
        // int-offline-book-keyin instead of appraisal-book-verification, which this path skips.
        //
        // ALWAYS send isOfflineExternal, including `false`. TaskActivity's inputMappings loop only
        // copies keys that are PRESENT in the resume input, so omitting it on a later EXT/INT
        // decision would leave a stale `true` on the instance from an earlier EXTO — and every
        // subsequent route-back would then divert the case into the keyin queue instead of
        // appraisal-book-verification. Writing it unconditionally makes the flag track the CURRENT
        // decision. Both keys are declared in appraisal-assignment's inputMappings; TaskActivity
        // maps them onto workflow variables of the same name, preserving the bool.
        var isOfflineExternal = IsOfflineExternal(command.DecisionTaken);

        var input = new Dictionary<string, object>
        {
            ["selectedCompanyId"] = command.AssigneeCompanyId ?? string.Empty,
            ["selectedCompanyName"] = command.AssigneeCompanyName ?? string.Empty,
            ["assignmentMethod"] = command.AssignmentMethod,
            ["decisionTaken"] = command.DecisionTaken,
            ["internalFollowupStaffId"] = command.InternalAppraiserId ?? string.Empty,
            ["internalFollowupMethod"] = command.InternalFollowupAssignmentMethod ?? string.Empty,
            ["isOfflineExternal"] = isOfflineExternal
        };

        if (isOfflineExternal)
            input["assignmentType"] = "External";

        // Pin the admin-selected internal appraiser (the EXECUTOR, carried in AssigneeUserId) onto
        // the activity that person will actually work; otherwise that activity's default
        // round-robin strategy picks someone else. Both internal-facing paths need this:
        //   INT  → int-appraisal-execution (the in-house appraisal itself)
        //   EXTO → int-offline-book-keyin  (keying in the external company's book)
        // Leaving AssigneeUserId empty means the admin chose round-robin, so no override is set and
        // the activity's own strategy selects the assignee.
        //
        // Note: InternalAppraiserId is the separate followup/checker field and feeds
        // internalFollowupStaffId above — it is NOT the executor.
        var executorActivityId = isOfflineExternal
            ? "int-offline-book-keyin"
            : string.Equals(command.DecisionTaken, "INT", StringComparison.OrdinalIgnoreCase)
                ? "int-appraisal-execution"
                : null;

        IReadOnlyDictionary<string, WorkflowAssigneeOverride>? overrides = null;
        if (executorActivityId is not null && !string.IsNullOrEmpty(command.AssigneeUserId))
        {
            overrides = new Dictionary<string, WorkflowAssigneeOverride>
            {
                [executorActivityId] = new WorkflowAssigneeOverride(
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

    /// <summary>
    /// "EXTO" — external company engaged outside the system, book keyed in by internal staff.
    /// Kept as a named predicate so the literal lives in one place alongside the
    /// route_external_offline decision condition in appraisal-workflow.json.
    /// </summary>
    private static bool IsOfflineExternal(string? decisionTaken) =>
        string.Equals(decisionTaken, "EXTO", StringComparison.OrdinalIgnoreCase);
}
