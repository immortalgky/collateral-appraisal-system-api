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
    /// <param name="knownUsernames">
    /// Usernames that resolve to a real user. When supplied, roster members outside this set are
    /// reported as failures — they would be counted into the round's member total but could never
    /// cast a vote. Pass null to skip the check (the domain has no way to resolve users itself).
    /// </param>
    public static IReadOnlyList<string> Check(
        IReadOnlyList<MeetingMember> roster,
        Committee committee,
        IReadOnlySet<string>? knownUsernames = null)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(committee);

        var failures = new List<string>();

        // Empty roster. Checked before quorum because a Percentage quorum of zero members is zero,
        // so the quorum rule below would pass it. An empty roster is never releasable: it cannot be
        // told apart from "no roster supplied" downstream — ApprovalActivity switches on
        // `overrideMembers.Count > 0`, so an empty one silently falls back to the committee's own
        // members, which is the exact substitution this whole path exists to prevent.
        if (roster.Count == 0)
        {
            failures.Add("the meeting has no members");
            return failures;
        }

        // Members who do not resolve to a real user. They still count toward the round's member
        // total — and therefore raise the majority denominator (MajorityRule evaluates against ALL
        // members, not votes cast) — while never being able to vote.
        if (knownUsernames is not null)
        {
            var unresolved = roster
                .Where(m => !knownUsernames.Contains(m.UserId))
                .Select(m => m.UserId)
                .ToList();

            if (unresolved.Count > 0)
                failures.Add($"no such user: {string.Join(", ", unresolved)}");
        }

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
