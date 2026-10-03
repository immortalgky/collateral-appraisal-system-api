namespace Workflow.Meetings.Domain;

/// <summary>
/// The default committee a meeting is snapshotted from. <c>CreateMeeting</c> always uses it;
/// <c>BulkCreateMeetings</c> uses it unless the request names another committee. Whichever one is
/// used is recorded on <see cref="Meeting.CommitteeId"/>, and that — not this constant — is what the
/// release gate checks the roster against and what released items are approved under.
/// </summary>
public static class MeetingCommittee
{
    public const string WithMeetingCode = "COMMITTEE_WITH_MEETING";
}
