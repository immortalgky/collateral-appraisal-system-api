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
///
/// Counts are taken of the roster's VOTING members, since that is the subset
/// <see cref="Meeting.ReleaseItem"/> actually hands to the round — the Secretary is excluded. The
/// full roster is used only where membership itself is what matters (the username check).
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

        // Everything below counts VOTERS, not roster members. Release hands the approval round only
        // the members who can vote (the Secretary is excluded — see Meeting.ReleaseItem), so the
        // roster total would overstate the set the round actually runs with: a roster of
        // Chairman + Secretary + UW clears a quorum of 3 but opens a round with 2 voters that can
        // never reach it. Counting the same subset the round receives keeps the two in step.
        var voters = roster.Where(m => CommitteeMemberPositions.CanVote(m.Position)).ToList();

        // Members who do not resolve to a real user. Checked against the FULL roster: a bad username
        // is worth reporting whether or not that member votes. Voting ones still count toward the
        // round's member total — and therefore raise the majority denominator (MajorityRule
        // evaluates against ALL members, not votes cast) — while never being able to vote.
        //
        // Runs BEFORE the no-voters return so both problems surface in one message. An all-secretary
        // roster that also carries a typo'd username would otherwise report only "no voting members",
        // and the secretary would discover the bad username on the next attempt instead of this one.
        if (knownUsernames is not null)
        {
            var unresolved = roster
                .Where(m => !knownUsernames.Contains(m.UserId))
                .Select(m => m.UserId)
                .ToList();

            if (unresolved.Count > 0)
                failures.Add($"no such user: {string.Join(", ", unresolved)}");
        }

        // No voting members. Returns early for the same reason the empty-roster guard does:
        // ApprovalActivity switches on `overrideMembers.Count > 0`, so an empty voting roster
        // silently falls back to the committee's own members — the substitution this path prevents.
        // Everything past here divides by the voter count, which would be meaningless at zero.
        if (voters.Count == 0)
        {
            failures.Add("the meeting has no voting members (the secretary does not vote)");
            return failures;
        }

        // Quorum. The same rule the approval round applies, against the same member count it will
        // run with (the voters), so this check and that round can never disagree. Only a Fixed
        // quorum can fail here — a Percentage of the voters is satisfiable by construction.
        var requiredQuorum = QuorumRule.Required(committee.QuorumType, committee.QuorumValue, voters.Count);
        if (voters.Count < requiredQuorum)
            failures.Add($"{voters.Count} voting member(s) but quorum requires {requiredQuorum}");

        // Majority. Only FixedCount can be unreachable — the proportional types are taken of the
        // voters themselves and so are satisfiable by construction, exactly like a Percentage quorum.
        if (committee.MajorityType == MajorityType.FixedCount && committee.MajorityValue > voters.Count)
            failures.Add(
                $"{committee.MajorityValue} approve vote(s) required but the roster has only {voters.Count} voting member(s)");

        foreach (var condition in committee.Conditions.Where(c => c.IsActive).OrderBy(c => c.Priority))
        {
            switch (condition.ConditionType)
            {
                // The condition is evaluated against the ROLE recorded on each vote, which is the
                // member's meeting position. If nobody on the roster holds the required position,
                // no vote can ever carry that role.
                case ConditionType.RoleRequired
                    when !string.IsNullOrWhiteSpace(condition.RoleRequired)
                         && !voters.Any(m => string.Equals(
                             m.Position.ToString(), condition.RoleRequired, StringComparison.OrdinalIgnoreCase)):
                    failures.Add($"no voting member holds the required role {condition.RoleRequired}");
                    break;

                case ConditionType.MinVotes
                    when condition.MinVotesRequired > voters.Count:
                    failures.Add(
                        $"{condition.MinVotesRequired} approve vote(s) required but the roster has only {voters.Count} voting member(s)");
                    break;
            }
        }

        return failures;
    }
}
