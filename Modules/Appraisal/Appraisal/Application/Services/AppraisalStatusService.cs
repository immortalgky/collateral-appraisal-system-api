using Appraisal.Domain.Appraisals;
using Appraisal.Contracts.Services;

namespace Appraisal.Application.Services;

/// <summary>
/// Implements cross-module status updates for the Workflow module's submission pipeline.
/// Maps target status strings to domain methods on the Appraisal aggregate.
/// </summary>
public class AppraisalStatusService(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork) : IAppraisalStatusService
{
    public async Task UpdateStatusAsync(Guid appraisalId, string targetStatus, string updatedBy, CancellationToken ct)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(appraisalId, ct)
                        ?? throw new InvalidOperationException($"Appraisal {appraisalId} not found");

        switch (targetStatus)
        {
            case "InProgress":
                appraisal.StartWork();
                break;
            case "UnderReview":
                appraisal.SubmitForReview();
                break;
            // Terminal Completed is owned exclusively by AppraisalApprovedIntegrationEventHandler →
            // MarkApprovedByCommittee. Allowing a workflow pipeline step to flip it here would
            // bypass the committee gate AND leave the AppraisalAssignment out of sync (no
            // assignment.Complete() side-effect on the legacy Appraisal.Complete() method).
            default:
                throw new ArgumentException($"Unsupported target status: {targetStatus}");
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    public Task UpdateAssignmentStatusAsync(Guid appraisalId, string targetStatus, string updatedBy,
        CancellationToken ct)
    {
        // Assignment-status transitions are now driven exclusively by:
        //   1. The synchronous command handlers (Pending → Assigned), which lock the admin screen.
        //   2. WorkflowTransitionedIntegrationEventHandler (Assigned → InProgress → UnderReview →
        //      Verified) reacting to workflow activity transitions.
        //   3. MarkApprovedByCommittee (Verified → Completed) on the committee approval event.
        // Allowing a workflow pipeline step to short-circuit any of those routes would create the
        // exact dual-ownership ambiguity the new lifecycle was designed to eliminate, so this
        // method now refuses every target rather than silently breaking invariants.
        throw new InvalidOperationException(
            $"UpdateAssignmentStatusAsync is no longer supported (received target '{targetStatus}'). " +
            "Assignment-status transitions are owned by the workflow event handler and the committee " +
            "approval handler. Remove the UpdateAssignmentStatus pipeline step from workflow definitions.");
    }
}
