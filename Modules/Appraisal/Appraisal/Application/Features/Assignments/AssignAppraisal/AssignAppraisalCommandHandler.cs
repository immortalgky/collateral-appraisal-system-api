using Appraisal.Application.Features.Fees.GetFeePreview;
using Appraisal.Application.Features.Quotations.AutoAssign;
using Appraisal.Application.Services;
using Shared.Identity;
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
    IWorkflowRelayService workflowRelayService,
    IQuotationRepository quotationRepository,
    IQuotationAutoAssignStateReader workflowOwnership,
    ISender sender,
    ICurrentUserService currentUser)
    : ICommandHandler<AssignAppraisalCommand, AssignAppraisalResult>
{
    /// <summary>Stable <c>errorCode</c> of the 400 when the assignment would bill no base appraisal fee (01).</summary>
    public const string FeeBaseMissingCode = "FeeBaseMissing";

    public async Task<AssignAppraisalResult> Handle(
        AssignAppraisalCommand command,
        CancellationToken cancellationToken)
    {
        // The actor always comes from the server (the authenticated user's bank code); the command carries no actor.
        // Quotation auto-assign runs inside the same request, so it resolves to the same user.
        // Trimmed once, so the row, the workflow's completedBy and the override's OverrideBy all carry the same value.
        var userCode = currentUser.UserCode;
        if (string.IsNullOrWhiteSpace(userCode))
            throw new ForbiddenException("The current user could not be determined, so the assignment cannot be recorded. Sign in again.");
        var assignedBy = userCode.Trim();

        // Validate that a pending assignment exists for this appraisal. The row acted on is the appraisal's CURRENT one
        // (AppraisalAssignmentElection) — the same row the fee preview, the guard below and the consumers use — and it
        // must be Pending; an older Pending row beside a newer live one is not it.
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        var pendingAssignment = appraisal.Assignments.Current() is { } current && current.AssignmentStatus == AssignmentStatus.Pending
            ? current
            : throw new BadRequestException(
                $"No pending assignment found for appraisal '{command.AppraisalId}'. " +
                "The workflow task must be in Pending status to accept an assignment.");

        // The relay resumes command.WorkflowInstanceId and nothing else ties it to this appraisal, so a
        // caller could otherwise resume (and bill the winner check against) a different appraisal's workflow.
        if (!await workflowOwnership.WorkflowInstanceBelongsToAppraisalAsync(
                command.AppraisalId, command.WorkflowInstanceId, cancellationToken))
            throw new BadRequestException("Workflow instance does not belong to this appraisal");

        // A "Quotation" assignment bills the quotation the company WON (CompanyAssignedIntegrationEventHandler).
        // That consumer runs after the workflow has already moved on, so a non-winner has to be refused here,
        // before the resume, or the case is left stuck with a failed message. Runs after the ownership checks so
        // the differing error cannot be used to probe quotations of appraisals the caller has no business with.
        // Allowlist: only EXT and P (admin-proceed-external) go on to company selection. C (reject) and R (request
        // more info) must still work for a non-winner, and INT/EXTO name no company. P can also route to
        // int-appraisal-execution when assignmentType is Internal, which this handler cannot see; "P + Internal +
        // Quotation method" is an incoherent combination and is guarded anyway.
        if (string.Equals(command.AssignmentMethod, "Quotation", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(command.DecisionTaken, DecisionExternal, StringComparison.OrdinalIgnoreCase)
                || string.Equals(command.DecisionTaken, DecisionProceed, StringComparison.OrdinalIgnoreCase)))
        {
            if (!Guid.TryParse(command.AssigneeCompanyId, out var winnerCompanyId))
                throw new BadRequestException("A company is required for a Quotation assignment.");

            // Only "did this company win" is asked here; whether the winner's price is usable is part of the
            // base-fee rule below, which also lets an admin's own 01 stand in for a missing price.
            if (!await quotationRepository.HasFinalizedWonByAsync(
                    command.AppraisalId, winnerCompanyId, cancellationToken))
                throw new BadRequestException("The selected company did not win a finalized quotation for this appraisal.");
        }

        // Reject (C) and request-more-info (R) assign nobody: they need no base fee and must not credit the admin.
        var isAssigning = IsAssigningDecision(command.DecisionTaken, out var decision);
        if (isAssigning)
            await EnsureBaseFeeAsync(command, decision, cancellationToken);

        // The admin's identity is only known here: the consumers that later call Assign() are workflow-driven, and
        // used to stamp "System". Recorded with the remark, in the same durable save before the resume.
        if (isAssigning)
            pendingAssignment.SetAssigner(assignedBy);

        // Persist the admin remark onto the row BEFORE resuming the workflow. Committing here (rather
        // than relying on a transactional marker) guarantees the value is durable before the relay
        // triggers the downstream CompanyAssigned/InternalAssigned handlers — .Assign() there does not
        // touch Remark, so it survives. This handler otherwise stays a pure relay (no transactional marker).
        if (command.Remark is not null)
            pendingAssignment.SetRemark(command.Remark);

        await unitOfWork.SaveChangesAsync(cancellationToken);

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
            : string.Equals(command.DecisionTaken, DecisionInternal, StringComparison.OrdinalIgnoreCase)
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
                    OverrideBy: assignedBy)
            };
        }

        await workflowRelayService.ResumeWorkflowAsync(
            command.WorkflowInstanceId,
            "appraisal-assignment",
            assignedBy,
            input,
            overrides,
            cancellationToken);

        return new AssignAppraisalResult(pendingAssignment.Id);
    }

    // Workflow decision codes (appraisal-workflow.json, appraisal-assignment decisionConditions).
    private const string DecisionExternal = "EXT";
    private const string DecisionInternal = "INT";
    private const string DecisionExternalOffline = "EXTO";
    private const string DecisionProceed = "P";

    /// <summary>EXT / INT / EXTO / P hand the case to someone; C (reject) and R (request more info) do not.</summary>
    private static bool IsAssigningDecision(string? decisionTaken, out string decision)
    {
        decision = decisionTaken?.Trim().ToUpperInvariant() ?? string.Empty;
        return decision is DecisionExternal or DecisionInternal or DecisionExternalOffline or DecisionProceed;
    }

    /// <summary>
    /// Every assignment (internal, external, off-system; any method) must bill a base appraisal fee (01) above 0:
    /// the admin's own 01, or the system base that materialisation will create. Judged by the fee preview, which
    /// shares its resolution with AssignmentFeeService (tier / chained inspection fee / quotation winner price; a
    /// blank shell or no fee row prices nothing), so what is refused here is exactly what would otherwise be
    /// assigned without a base. Reject (C) and request-more-info (R) assign nobody and are not asked.
    /// The consumer-side race (the admin removes the 01 after this check) stays warn-and-continue in the fee service.
    /// </summary>
    private async Task EnsureBaseFeeAsync(AssignAppraisalCommand command, string decision, CancellationToken ct)
    {
        var isQuotation = string.Equals(command.AssignmentMethod, "Quotation", StringComparison.OrdinalIgnoreCase);
        var type = decision switch
        {
            DecisionExternal => "external",
            DecisionInternal => "internal",
            DecisionExternalOffline => "external-offline",
            _ => isQuotation ? "external" : null // proceed: the row's own type; only a quotation needs the company
        };

        var preview = await sender.Send(
            new GetFeePreviewQuery(
                command.AppraisalId,
                type,
                isQuotation ? "quotation" : "manual",
                Guid.TryParse(command.AssigneeCompanyId, out var companyId) ? companyId : null),
            ct);

        if (!preview.HasBillableBase)
            throw new BadRequestException(
                "Cannot assign: no base appraisal fee (01) would be billed. Enter the base fee on the fee page, or " +
                "make sure the fee source (selling-price tier, prior inspection fee or quotation price) gives a price above 0.",
                null,
                FeeBaseMissingCode);
    }

    /// <summary>
    /// "EXTO" — external company engaged outside the system, book keyed in by internal staff.
    /// Kept as a named predicate so the literal lives in one place alongside the
    /// route_external_offline decision condition in appraisal-workflow.json.
    /// </summary>
    private static bool IsOfflineExternal(string? decisionTaken) =>
        string.Equals(decisionTaken, DecisionExternalOffline, StringComparison.OrdinalIgnoreCase);
}
