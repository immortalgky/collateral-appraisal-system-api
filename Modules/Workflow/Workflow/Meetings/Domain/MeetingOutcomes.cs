namespace Workflow.Meetings.Domain;

/// <summary>
/// String constants for meeting outcome values passed to workflow resume input.
/// Consumed by <see cref="Activities.MeetingActivity"/> and downstream transition conditions.
/// </summary>
public static class MeetingOutcomes
{
    /// <summary>Item was released to meeting members as parallel approvers.</summary>
    public const string Released = "released";

    /// <summary>Item was routed back to the appraisal team for rework.</summary>
    public const string RouteBack = "routeback";

    /// <summary>Meeting was cancelled; workflow stays paused until reassigned.</summary>
    public const string Cancelled = "cancelled";
}
