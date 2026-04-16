namespace Workflow.Meetings.Domain;

public enum MeetingItemKind
{
    /// <summary>Item requires a secretary decision (Release or RouteBack) after the meeting ends.</summary>
    Decision,
    /// <summary>Item is an information acknowledgement from a sub-committee approval; no workflow gate.</summary>
    Acknowledgement
}
