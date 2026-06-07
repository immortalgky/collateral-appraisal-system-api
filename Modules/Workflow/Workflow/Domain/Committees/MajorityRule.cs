namespace Workflow.Domain.Committees;

/// <summary>
/// Single source of truth for the committee majority rule. The approve count is always evaluated
/// against the FULL committee (<paramref name="totalMembers"/>), not the votes cast — so "Simple"
/// means a majority of all members and "TwoThirds" two-thirds of all members.
/// Used by both <see cref="Committee.HasMajority"/> (domain) and the approval engine
/// (<c>ApprovalActivity.CheckMajority</c>) so the two cannot drift.
/// </summary>
public static class MajorityRule
{
    public static bool IsMet(MajorityType type, int approveCount, int totalMembers) =>
        type switch
        {
            MajorityType.Simple => approveCount > totalMembers / 2.0,
            MajorityType.TwoThirds => approveCount >= Math.Ceiling(totalMembers * 2.0 / 3.0),
            MajorityType.Unanimous => approveCount == totalMembers,
            _ => false
        };
}
