namespace Workflow.Meetings.Domain;

public enum MeetingStatus
{
    New,
    InvitationSent,
    RoutedBack,
    Ended,
    Cancelled,

    /// <summary>
    /// Every Decision item has been released but at least one committee approval round is still
    /// open. Live, not finished: moves to <see cref="Ended"/> when the last round concludes.
    /// Appended last — the column is stored as a string, but keep ordinals stable anyway.
    /// </summary>
    AwaitingVotes
}
