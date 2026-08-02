namespace Workflow.Domain.Committees;

/// <summary>
/// Single source of truth for the committee majority rule. The proportional types evaluate the
/// approve count against the FULL committee (<paramref name="totalMembers"/>), not the votes cast
/// — so "Simple" means a majority of all members and "TwoThirds" two-thirds of all members.
/// Used by both <see cref="Committee.HasMajority"/> (domain) and the approval engine
/// (<c>ApprovalActivity.CheckMajority</c>, <c>ApprovalListProjection.CheckMajority</c>) so they
/// cannot drift.
/// </summary>
public static class MajorityRule
{
    /// <param name="value">
    /// Only read for <see cref="MajorityType.FixedCount"/>: the absolute number of approvals that
    /// resolves the round, independent of how many members there are. Ignored by the proportional
    /// types, which is why it is optional — existing call sites keep compiling unchanged.
    /// </param>
    public static bool IsMet(MajorityType type, int approveCount, int totalMembers, int value = 0) =>
        type switch
        {
            MajorityType.Simple => approveCount > totalMembers / 2.0,
            MajorityType.TwoThirds => approveCount >= Math.Ceiling(totalMembers * 2.0 / 3.0),
            MajorityType.Unanimous => approveCount == totalMembers,
            // A non-positive threshold would make every round approve on zero votes, so it is
            // treated as unmet rather than trusted. Committee.Create/Update reject it up front.
            MajorityType.FixedCount => value > 0 && approveCount >= value,
            _ => false
        };
}
