namespace Workflow.Meetings.Domain;

public interface IMeetingRepository
{
    Task<Meeting?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Meeting?> GetByIdWithItemsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Loads meeting with Items and Members — for Release/RouteBack and detail views.</summary>
    Task<Meeting?> GetByIdForDecisionAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns the earliest-scheduled meeting that starts before <paramref name="startAt"/>, is still
    /// active (Status not Ended/Cancelled), and whose scheduled end time has not passed (EndAt &gt; now),
    /// excluding <paramref name="excludeMeetingId"/>. Used to block cut-off while an earlier meeting is
    /// still in progress. An earlier meeting that has ended, been cancelled, or whose scheduled window
    /// has already elapsed does not block. Returns null if none exists.
    /// </summary>
    Task<Meeting?> GetEarlierActiveMeetingAsync(DateTime startAt, DateTime now, Guid excludeMeetingId, CancellationToken ct = default);

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
