namespace Workflow.Domain.Committees;

/// <summary>
/// Single source of truth for how many votes a round needs before it can resolve.
/// Used by the approval engine (<c>ApprovalActivity.GetRequiredQuorum</c>) and by the
/// meeting release gate (<c>MeetingRosterEligibility</c>) so the number a secretary is
/// checked against is the number the round will actually demand.
/// </summary>
public static class QuorumRule
{
    /// <param name="memberCount">
    /// The members the round will run with — the committee's active members normally, or the
    /// meeting roster when it overrides them.
    /// </param>
    public static int Required(QuorumType type, int value, int memberCount) =>
        type switch
        {
            QuorumType.Fixed => value,
            QuorumType.Percentage => (int)Math.Ceiling(memberCount * value / 100.0),
            _ => memberCount
        };

    /// <summary>String overload for the engine, which round-trips the type name through JSON.</summary>
    public static int Required(string type, int value, int memberCount) =>
        Enum.TryParse<QuorumType>(type, ignoreCase: true, out var parsed)
            ? Required(parsed, value, memberCount)
            : memberCount;
}
