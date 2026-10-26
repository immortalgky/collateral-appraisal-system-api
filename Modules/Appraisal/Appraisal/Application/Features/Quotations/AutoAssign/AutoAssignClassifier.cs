using AssignmentStatusVo = Appraisal.Domain.Appraisals.AssignmentStatus;

namespace Appraisal.Application.Features.Quotations.AutoAssign;

/// <summary>Outcome null means the appraisal is ready to assign.</summary>
public sealed record AutoAssignClassification(string? Outcome, string? Reason, string? Message)
{
    public static readonly AutoAssignClassification Ready = new(null, null, null);
}

/// <summary>
/// The single decision ladder for "what would auto-assign do with this appraisal", shared by the
/// post-finalize loop and the preview so they cannot drift. Order matters and is part of the contract.
/// Staff eligibility and the assign call itself are not decided here.
/// </summary>
public static class AutoAssignClassifier
{
    public static AutoAssignClassification Classify(
        AppraisalAssignState? state, bool winnerAssignable, bool priced, string? user)
    {
        if (state is null)
            return new(AutoAssignOutcome.Skipped, AutoAssignReason.Deleted, "Appraisal was deleted");

        if (state.WorkflowEnded)
            return new(AutoAssignOutcome.Skipped, AutoAssignReason.WorkflowEnded, "No active workflow for this appraisal");

        if (state.AssignmentStatus is null)
            return new(AutoAssignOutcome.Skipped, AutoAssignReason.NoAssignment, "No assignment found for this appraisal");

        // The return-to-queue reset of the assignment runs in an async consumer, so a task can be back here
        // before its assignment is Pending again. That needs a person, not a silent skip.
        if (state.AssignmentStatus != AssignmentStatusVo.Pending.Code
            && state.PendingActivityId == AppraisalAssignState.AssignmentActivityId)
            return new(AutoAssignOutcome.Failed, AutoAssignReason.AssignmentNotReset,
                $"Back at appraisal-assignment but the assignment record is not Pending yet (status '{state.AssignmentStatus}'). " +
                "Assign it on the Administration page.");

        if (state.AssignmentStatus != AssignmentStatusVo.Pending.Code)
            return new(AutoAssignOutcome.Skipped, AutoAssignReason.NotPending,
                $"Assignment is not pending (status '{state.AssignmentStatus}')");

        if (state.PendingActivityId != AppraisalAssignState.AssignmentActivityId)
            return new(AutoAssignOutcome.Parked, AutoAssignReason.NotAtAssignment,
                $"Not at appraisal-assignment, currently at '{state.PendingActivityId ?? "no active task"}'");

        // Resuming a task someone else holds would pull it from under them.
        if (state.LockedByOther(user) is { } lockedBy)
            return new(AutoAssignOutcome.Failed, AutoAssignReason.TaskHeld, $"Another admin is working on this task ({lockedBy})");

        if (state.AssignedToOther(user) is { } assignedTo)
            return new(AutoAssignOutcome.Failed, AutoAssignReason.TaskHeld, $"Task is assigned to another admin ({assignedTo})");

        // Normal completion of appraisal-assignment is blocked by RequireDocumentFollowupCleared; the relay
        // skips the pipeline, so the same rule is applied here.
        if (state.HasOpenFollowup)
            return new(AutoAssignOutcome.Failed, AutoAssignReason.DocumentFollowupOpen,
                "An open document follow-up must be cleared first. Assign it on the Administration page.");

        if (!winnerAssignable)
            return new(AutoAssignOutcome.Failed, AutoAssignReason.WinnerUnavailable, "Winning company is not available for assignment");

        // Without the winner's own item the fee resolver would fall back to the whole quotation's total. Unless the
        // admin already keyed the base fee: that outranks the quotation at assignment, so no price is needed.
        if (!priced && !state.HasAdminBaseFee)
            return new(AutoAssignOutcome.Failed, AutoAssignReason.Unpriced, "Winning company has no price for this appraisal");

        return AutoAssignClassification.Ready;
    }
}
