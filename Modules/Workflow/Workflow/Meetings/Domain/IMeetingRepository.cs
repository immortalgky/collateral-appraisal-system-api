namespace Workflow.Meetings.Domain;

public interface IMeetingRepository
{
    Task<Meeting?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Meeting?> GetByIdWithItemsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Loads meeting with Items and Members — for Release/RouteBack and detail views.</summary>
    Task<Meeting?> GetByIdForDecisionAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns the lowest-MeetingNoSeq meeting in the same year that is earlier than the given seq
    /// and has not yet ended (Status != Ended, != Cancelled). Used to enforce sequential cut-off
    /// ordering: a meeting cannot be cut off until every earlier meeting has ended. Returns null
    /// if none exists.
    /// </summary>
    Task<Meeting?> GetEarlierUnendedMeetingAsync(int meetingNoYear, int meetingNoSeq, CancellationToken ct = default);

    /// <summary>
    /// Returns the first non-cancelled meeting whose scheduled window overlaps the half-open
    /// interval [<paramref name="startAt"/>, <paramref name="endAt"/>). Used to prevent
    /// double-booking on creation/update. <paramref name="excludeMeetingId"/> is skipped from the
    /// search — pass the current meeting's id when validating an update. Returns null if no
    /// overlap exists.
    /// </summary>
    Task<Meeting?> GetOverlappingMeetingAsync(DateTime startAt, DateTime endAt, Guid? excludeMeetingId = null, CancellationToken ct = default);

    /// <summary>
    /// Returns the non-cancelled scheduled meeting with the latest <c>StartAt</c>. Used to enforce
    /// that a newly-created meeting (which always gets the next sequence number) is not scheduled
    /// earlier than any previously-numbered meeting. Returns null if no scheduled meeting exists.
    /// </summary>
    Task<Meeting?> GetLatestScheduledMeetingAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the non-cancelled scheduled meeting with the latest <c>StartAt</c> whose meeting
    /// number is strictly less than (<paramref name="meetingNoYear"/>, <paramref name="meetingNoSeq"/>).
    /// Used on update to ensure the meeting is not moved earlier than any lower-numbered meeting.
    /// </summary>
    Task<Meeting?> GetLatestScheduledBeforeNumberAsync(int meetingNoYear, int meetingNoSeq, CancellationToken ct = default);

    /// <summary>
    /// Returns the non-cancelled scheduled meeting with the earliest <c>StartAt</c> whose meeting
    /// number is strictly greater than (<paramref name="meetingNoYear"/>, <paramref name="meetingNoSeq"/>).
    /// Used on update to ensure the meeting is not moved later than any higher-numbered meeting.
    /// </summary>
    Task<Meeting?> GetEarliestScheduledAfterNumberAsync(int meetingNoYear, int meetingNoSeq, CancellationToken ct = default);

    Task AddAsync(Meeting meeting, CancellationToken ct = default);
}
