namespace Workflow.Meetings.Domain;

/// <summary>
/// The committee that governs meetings. Only the top appraisal tier routes through a meeting
/// (<c>approval-tier-switch</c> → <c>pending-meeting</c> in appraisal-workflow.json), so a
/// meeting is always snapshotted from — and its released items always approved by — this one
/// committee. Shared by <c>CreateMeeting</c>, <c>BulkCreateMeetings</c> and the release gate so
/// the roster is checked against the same committee it was copied from.
/// </summary>
public static class MeetingCommittee
{
    public const string WithMeetingCode = "COMMITTEE_WITH_MEETING";
}
