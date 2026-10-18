using Workflow.Meetings.Domain;

namespace Workflow.Data;

public static class OpenMeetingItemQuery
{
    /// <summary>
    /// This instance's Decision items still owed to a meeting — RoutedBack (rework) or Pending (a
    /// secretary recall) — on a live meeting, newest first. Excludes Cancelled meetings:
    /// Meeting.Cancel() leaves its items behind, so a stale row from a cancel-and-reschedule must
    /// not win. MeetingActivity re-enters on the first row and the engine redirects to it; sharing
    /// one predicate keeps them in step. MeetingActivity additionally narrows by AppraisalId — a
    /// no-op today, since a workflow instance carries exactly one appraisal.
    /// </summary>
    public static IQueryable<MeetingItem> OpenMeetingItems(this WorkflowDbContext db, Guid workflowInstanceId) =>
        db.MeetingItems
            .Where(mi => mi.WorkflowInstanceId == workflowInstanceId
                         && mi.Kind == MeetingItemKind.Decision
                         && mi.ItemDecision != ItemDecision.Released
                         && db.Meetings.Any(m => m.Id == mi.MeetingId && m.Status != MeetingStatus.Cancelled))
            .OrderByDescending(mi => mi.AddedAt);
}
