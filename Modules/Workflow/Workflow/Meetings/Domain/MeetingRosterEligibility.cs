using Workflow.Domain.Committees;

namespace Workflow.Meetings.Domain;

/// <summary>
/// Checks that a meeting's roster can actually carry the approval round that follows a release.
///
/// Once an item is released the roster REPLACES the committee's members in the approval activity,
/// but the rules the round is judged by — quorum and the committee's approval conditions — still
/// come from the committee. A roster that cannot satisfy them produces a round that can never
/// resolve, with the appraisal stuck in Committee Approval and no error anywhere. The release
/// endpoint runs this first and refuses instead.
/// </summary>
public static class MeetingRosterEligibility
{
    /// <summary>
    /// Returns one message per unsatisfiable rule; empty means the roster can carry the round.
    /// </summary>
    public static IReadOnlyList<string> Check(IReadOnlyList<MeetingMember> roster, Committee committee)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(committee);

        var failures = new List<string>();

        // Quorum. The same rule the approval round applies, against the same member count it will
        // run with (the roster), so this check and that round can never disagree. Only a Fixed
        // quorum can fail here — a Percentage of the roster is satisfiable by construction.
        var requiredQuorum = QuorumRule.Required(committee.QuorumType, committee.QuorumValue, roster.Count);
        if (roster.Count < requiredQuorum)
            failures.Add($"{roster.Count} member(s) but quorum requires {requiredQuorum}");

        foreach (var condition in committee.Conditions.Where(c => c.IsActive).OrderBy(c => c.Priority))
        {
            switch (condition.ConditionType)
            {
                // The condition is evaluated against the ROLE recorded on each vote, which is the
                // member's meeting position. If nobody on the roster holds the required position,
                // no vote can ever carry that role.
                case ConditionType.RoleRequired
                    when !string.IsNullOrWhiteSpace(condition.RoleRequired)
                         && !roster.Any(m => string.Equals(
                             m.Position.ToString(), condition.RoleRequired, StringComparison.OrdinalIgnoreCase)):
                    failures.Add($"no member holds the required role {condition.RoleRequired}");
                    break;

                case ConditionType.MinVotes
                    when condition.MinVotesRequired > roster.Count:
                    failures.Add(
                        $"{condition.MinVotesRequired} approve vote(s) required but the roster has only {roster.Count} member(s)");
                    break;
            }
        }

        return failures;
    }
}
