using Appraisal.Domain.Appraisals;

namespace Appraisal.Tests.Domain;

/// <summary>
/// Unit tests covering the three SLA-related invariants on AppraisalAssignment:
/// 1. SubmittedAt is stamped only once (first InProgress → UnderReview).
/// 2. MarkUnderReview from Verified does not stamp SubmittedAt.
/// 3. SetSlaDueDate is a no-op after the first call.
/// </summary>
public class AppraisalAssignmentSlaTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static AppraisalAssignment CreateAssignedAssignment()
    {
        var a = AppraisalAssignment.Create(
            appraisalId: Guid.NewGuid(),
            assignmentType: "External",
            assigneeCompanyId: Guid.NewGuid().ToString(),
            assignmentMethod: "Manual",
            assignedBy: "system");

        // Transition to Assigned (simulates CompanyAssignedIntegrationEventHandler)
        a.Assign(
            assignmentType: "External",
            assigneeCompanyId: Guid.NewGuid().ToString(),
            assignmentMethod: "Manual",
            assignedBy: "system");

        return a;
    }

    // ---------------------------------------------------------------------------
    // Test 1: MarkUnderReview after rework does NOT re-stamp SubmittedAt
    // ---------------------------------------------------------------------------

    [Fact]
    public void MarkUnderReview_AfterRework_DoesNotReStampSubmittedAt()
    {
        var assignment = CreateAssignedAssignment();

        // InProgress → first submission
        assignment.StartWork();
        assignment.MarkUnderReview();
        var stamp1 = assignment.SubmittedAt;
        Assert.NotNull(stamp1);

        // Bank sends it back (Rework returns to InProgress)
        assignment.Rework("needs revision", DateTime.Now);
        Assert.Equal(AssignmentStatus.InProgress, assignment.AssignmentStatus);

        // Second submission — SubmittedAt must not change
        assignment.MarkUnderReview();
        Assert.Equal(stamp1, assignment.SubmittedAt);
    }

    // ---------------------------------------------------------------------------
    // Test 2: MarkUnderReview from Verified does NOT stamp SubmittedAt
    // ---------------------------------------------------------------------------

    [Fact]
    public void MarkUnderReview_FromVerified_DoesNotStampSubmittedAt()
    {
        var assignment = CreateAssignedAssignment();

        // Reach Verified via the full domain path: Assigned → InProgress → UnderReview → Verified.
        // stamp1 captures the SubmittedAt value from the first InProgress → UnderReview transition.
        assignment.StartWork();
        assignment.MarkUnderReview();
        var stamp1 = assignment.SubmittedAt;
        assignment.MarkVerified();

        // Meeting/approval routes back to book-verification — demotes Verified → UnderReview.
        // This path does NOT represent an appraiser resubmit.
        assignment.MarkUnderReview();

        // SubmittedAt must remain at the original first-submission stamp
        Assert.Equal(stamp1, assignment.SubmittedAt);
    }

    // ---------------------------------------------------------------------------
    // Test 3: SetSlaDueDate second call is a no-op
    // ---------------------------------------------------------------------------

    [Fact]
    public void SetSlaDueDate_SecondCall_IsNoOp()
    {
        var assignment = AppraisalAssignment.Create(
            appraisalId: Guid.NewGuid(),
            assignmentType: "External",
            assignedBy: "system");

        var t1 = new DateTime(2026, 5, 20, 9, 0, 0);
        var t2 = new DateTime(2026, 5, 27, 9, 0, 0);

        assignment.SetSlaDueDate(t1);
        assignment.SetSlaDueDate(t2); // must be ignored

        Assert.Equal(t1, assignment.SLADueDate);
    }
}
