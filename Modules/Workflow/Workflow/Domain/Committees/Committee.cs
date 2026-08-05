namespace Workflow.Domain.Committees;

public class Committee : Aggregate<Guid>
{
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public QuorumType QuorumType { get; private set; }
    public int QuorumValue { get; private set; }
    public MajorityType MajorityType { get; private set; }

    /// <summary>
    /// The approval count that resolves the round when <see cref="MajorityType"/> is
    /// <see cref="MajorityType.FixedCount"/> — e.g. 3, meaning three approvals are enough
    /// regardless of how many members the committee has. Unused (0) for the proportional types.
    /// </summary>
    public int MajorityValue { get; private set; }

    /// <summary>
    /// Controls when the approval round resolves:
    ///   <see cref="VotingMode.WaitForAll"/> — every member must vote before the approve rule is
    ///   evaluated (consensus); quorum is ignored.
    ///   <see cref="VotingMode.Quorum"/> — resolve as soon as quorum + majority are met; the
    ///   still-open tasks of members who have not voted are closed out.
    /// </summary>
    public VotingMode VotingMode { get; private set; }

    private readonly List<CommitteeMember> _members = new();
    public IReadOnlyList<CommitteeMember> Members => _members.AsReadOnly();

    private readonly List<CommitteeThreshold> _thresholds = new();
    public IReadOnlyList<CommitteeThreshold> Thresholds => _thresholds.AsReadOnly();

    private readonly List<CommitteeApprovalCondition> _conditions = new();
    public IReadOnlyList<CommitteeApprovalCondition> Conditions => _conditions.AsReadOnly();

    private Committee() { }

    public static Committee Create(
        string name,
        string code,
        string? description,
        QuorumType quorumType,
        int quorumValue,
        MajorityType majorityType,
        VotingMode votingMode = VotingMode.WaitForAll,
        int majorityValue = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(quorumValue, 0, nameof(quorumValue));
        // No members exist yet at creation, so only the lower bound is checkable here; Update and
        // the round-start guard in ApprovalActivity cover "more approvals than there are members".
        RequirePositiveWhenFixedCount(majorityType, majorityValue);

        var committee = new Committee
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Code = code,
            Description = description,
            IsActive = true,
            QuorumType = quorumType,
            QuorumValue = quorumValue,
            MajorityType = majorityType,
            MajorityValue = majorityValue,
            VotingMode = votingMode
        };
        return committee;
    }

    public void Update(string name, string? description, QuorumType quorumType, int quorumValue,
        MajorityType majorityType, bool isActive, VotingMode votingMode = VotingMode.WaitForAll,
        int majorityValue = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        RequirePositiveWhenFixedCount(majorityType, majorityValue);

        // Members are known by now, so a threshold nobody could ever reach is rejected outright —
        // it would otherwise open a round that can never resolve and stalls with no error.
        // Counts VOTING members, the same subset MeetingRosterEligibility checks the roster against
        // at release; counting the Secretary here would accept a threshold that gate then refuses.
        var votingMembers = CountActiveVoters();
        if (majorityType == MajorityType.FixedCount && votingMembers > 0 && majorityValue > votingMembers)
            throw new ArgumentException(
                $"MajorityValue {majorityValue} exceeds the committee's {votingMembers} voting member(s); " +
                "the approval round could never reach it",
                nameof(majorityValue));

        Name = name;
        Description = description;
        QuorumType = quorumType;
        QuorumValue = quorumValue;
        MajorityType = majorityType;
        MajorityValue = majorityValue;
        IsActive = isActive;
        VotingMode = votingMode;
    }

    private static void RequirePositiveWhenFixedCount(MajorityType majorityType, int majorityValue)
    {
        if (majorityType == MajorityType.FixedCount && majorityValue <= 0)
            throw new ArgumentException(
                $"MajorityType {nameof(MajorityType.FixedCount)} requires a MajorityValue greater than 0",
                nameof(majorityValue));
    }

    public CommitteeMember AddMember(string userId, string memberName, CommitteeMemberPosition position,
        CommitteeAttendance attendance = CommitteeAttendance.Always)
    {
        var existing = _members.FirstOrDefault(m =>
            m.UserId == userId && m.IsActive);
        if (existing is not null)
            throw new InvalidOperationException($"User {userId} is already an active member of this committee");

        var member = CommitteeMember.Create(Id, userId, memberName, position, attendance);
        _members.Add(member);
        return member;
    }

    public void RemoveMember(Guid memberId)
    {
        var member = _members.FirstOrDefault(m => m.Id == memberId)
            ?? throw new NotFoundException($"CommitteeMember {memberId} not found");
        member.Deactivate();
    }

    /// <summary>
    /// Updates an existing committee member's position, attendance schedule, and active status.
    /// Throws <see cref="NotFoundException"/> if the member is not found.
    /// </summary>
    public void UpdateMember(Guid memberId, CommitteeMemberPosition position,
        CommitteeAttendance attendance, bool isActive)
    {
        var member = _members.FirstOrDefault(m => m.Id == memberId)
            ?? throw new NotFoundException($"CommitteeMember {memberId} not found");

        member.UpdatePosition(position);
        member.UpdateAttendance(attendance);
        if (isActive) member.Activate(); else member.Deactivate();
    }

    public CommitteeThreshold AddThreshold(decimal? minValue, decimal? maxValue, int priority)
    {
        var threshold = CommitteeThreshold.Create(Id, minValue, maxValue, priority);
        _thresholds.Add(threshold);
        return threshold;
    }

    /// <param name="roleRequired">
    /// For <see cref="ConditionType.RoleRequired"/>, the <see cref="CommitteeMemberPosition"/> that
    /// must cast an approving vote. It is matched — case-insensitively, as a plain string — against
    /// the role stamped on each <c>ApprovalVote</c>, which is the voter's
    /// <see cref="CommitteeMember.Position"/>. A value outside the enum therefore matches nothing:
    /// the condition can never be satisfied, and the approval round sits Pending forever with no
    /// error raised anywhere. Validated here rather than at the endpoint so every caller is covered.
    /// </param>
    public CommitteeApprovalCondition AddCondition(
        ConditionType conditionType, string? roleRequired, int? minVotesRequired,
        int priority, string? description)
    {
        RequireSatisfiableCondition(conditionType, roleRequired, minVotesRequired);

        var condition = CommitteeApprovalCondition.Create(
            Id, conditionType, roleRequired, minVotesRequired, priority, description);
        _conditions.Add(condition);
        return condition;
    }

    public void UpdateCondition(
        Guid conditionId, ConditionType conditionType, string? roleRequired,
        int? minVotesRequired, int priority, string? description, bool isActive)
    {
        var condition = _conditions.FirstOrDefault(c => c.Id == conditionId)
            ?? throw new NotFoundException($"CommitteeApprovalCondition {conditionId} not found");

        // Only an ACTIVE condition is evaluated, so an unsatisfiable one is harmless while
        // inactive — validate just the states that can actually block a round.
        if (isActive)
            RequireSatisfiableCondition(conditionType, roleRequired, minVotesRequired);

        condition.Update(conditionType, roleRequired, minVotesRequired, priority, description, isActive);
    }

    /// <summary>Soft-removes, mirroring <see cref="RemoveMember"/> — history keeps the row.</summary>
    public void RemoveCondition(Guid conditionId)
    {
        var condition = _conditions.FirstOrDefault(c => c.Id == conditionId)
            ?? throw new NotFoundException($"CommitteeApprovalCondition {conditionId} not found");
        condition.Deactivate();
    }

    /// <summary>
    /// Rejects a condition the approval round could never satisfy, which would otherwise stall the
    /// round with no error — the same class of guard as the MajorityValue check. Mirrors exactly
    /// what <c>ApprovalActivity.CheckApprovalConditions</c> reads: it compares RoleRequired against
    /// the voter's role case-insensitively, so a free-text role that matches no member silently
    /// fails forever.
    /// </summary>
    private void RequireSatisfiableCondition(
        ConditionType conditionType, string? roleRequired, int? minVotesRequired)
    {
        var activeMembers = GetActiveMembers();

        if (conditionType == ConditionType.RoleRequired)
        {
            if (string.IsNullOrWhiteSpace(roleRequired))
                throw new ArgumentException(
                    $"ConditionType {nameof(ConditionType.RoleRequired)} requires a role.",
                    nameof(roleRequired));

            // Name comparison, not Enum.TryParse: TryParse also accepts the numeric form
            // ("3" -> UW), and roleRequired is persisted as the RAW STRING, so "3" would pass
            // validation and then match no vote's role at runtime — CheckApprovalConditions
            // compares it as a plain case-insensitive string. That is the exact silent stall
            // this guard exists to prevent.
            // Advertises the requirable set, not the selectable one: the latter includes the
            // Secretary, whom the guard below refuses — so the error would name a value that a
            // retry with it copied verbatim would reject again.
            if (!CommitteeMemberPositions.TryParseName(roleRequired, out var position))
                throw new ArgumentException(
                    $"Invalid role '{roleRequired}'. Allowed values: " +
                    $"{CommitteeMemberPositions.RequirableNames}.",
                    nameof(roleRequired));

            if (!CommitteeMemberPositions.Selectable.Contains(position))
                throw new ArgumentException(
                    $"Role '{position}' is retired and can no longer be required. Allowed values: " +
                    $"{CommitteeMemberPositions.RequirableNames}.",
                    nameof(roleRequired));

            // The Secretary never casts an approval vote (Meeting.ReleaseItem excludes them from the
            // approver roster), and this condition is evaluated against the role recorded on a vote.
            // Requiring it would produce a round that can never satisfy the condition.
            if (!CommitteeMemberPositions.CanVote(position))
                throw new ArgumentException(
                    $"Role '{position}' does not vote, so the approval round could never satisfy " +
                    "this condition.",
                    nameof(roleRequired));

            if (activeMembers.Count > 0 && activeMembers.All(m => m.Position != position))
                throw new ArgumentException(
                    $"No active member holds the role '{position}'; the approval round could never " +
                    "satisfy this condition.",
                    nameof(roleRequired));

            return;
        }

        if (minVotesRequired is not > 0)
            throw new ArgumentException(
                $"ConditionType {nameof(ConditionType.MinVotes)} requires a MinVotesRequired greater than 0.",
                nameof(minVotesRequired));

        // Voting members, for the same reason as the MajorityValue guard in Update: votes are what
        // this threshold counts, and the Secretary casts none.
        var votingMembers = CountActiveVoters();
        if (votingMembers > 0 && minVotesRequired > votingMembers)
            throw new ArgumentException(
                $"MinVotesRequired {minVotesRequired} exceeds the committee's {votingMembers} " +
                "voting member(s); the approval round could never reach it.",
                nameof(minVotesRequired));
    }

    /// <summary>
    /// Active members who actually cast a vote — the denominator every approval threshold is
    /// judged against. See <see cref="CommitteeMemberPositions.CanVote"/>.
    /// </summary>
    private int CountActiveVoters() =>
        _members.Count(m => m.IsActive && CommitteeMemberPositions.CanVote(m.Position));

    public List<CommitteeMember> GetActiveMembers() =>
        _members.Where(m => m.IsActive).ToList();

    /// <summary>
    /// Returns active members filtered by parity of <paramref name="meetingSeq"/>.
    /// A member with <see cref="CommitteeAttendance.Always"/> is always included.
    /// A member with <see cref="CommitteeAttendance.Odd"/> is included when seq is odd.
    /// A member with <see cref="CommitteeAttendance.Even"/> is included when seq is even.
    /// </summary>
    public List<CommitteeMember> GetActiveMembers(int meetingSeq) =>
        _members.Where(m =>
            m.IsActive && (
                m.Attendance == CommitteeAttendance.Always ||
                (m.Attendance == CommitteeAttendance.Odd  && meetingSeq % 2 == 1) ||
                (m.Attendance == CommitteeAttendance.Even && meetingSeq % 2 == 0)
            )).ToList();

    public int GetRequiredQuorum()
    {
        var activeCount = GetActiveMembers().Count;
        return QuorumType switch
        {
            QuorumType.Fixed => QuorumValue,
            QuorumType.Percentage => (int)Math.Ceiling(activeCount * QuorumValue / 100.0),
            _ => activeCount
        };
    }

    public bool HasQuorum(int totalVotes)
    {
        return totalVotes >= GetRequiredQuorum();
    }

    /// <summary>
    /// Evaluates the approve rule against the FULL committee (<paramref name="totalMembers"/>),
    /// not merely the votes cast. <paramref name="totalVotes"/> is retained for signature symmetry
    /// but is no longer the denominator. Delegates to <see cref="MajorityRule"/> so the engine and
    /// the domain share one implementation.
    /// </summary>
    public bool HasMajority(int targetVoteCount, int totalVotes, int totalMembers)
        => MajorityRule.IsMet(MajorityType, targetVoteCount, totalMembers, MajorityValue);
}

public enum QuorumType
{
    Fixed,
    Percentage
}

public enum MajorityType
{
    /// <summary>More than half of ALL members approve.</summary>
    Simple,

    /// <summary>At least two-thirds of ALL members approve.</summary>
    TwoThirds,

    /// <summary>Every member approves.</summary>
    Unanimous,

    /// <summary>
    /// An absolute number of approvals — <see cref="Committee.MajorityValue"/> — regardless of
    /// member count. Note this replaces the majority bar only; whether the round resolves on the
    /// Nth approval or after everyone has voted is still governed by <see cref="VotingMode"/>.
    /// </summary>
    FixedCount
}

public enum VotingMode
{
    /// <summary>Every member must vote before the approve rule is evaluated (consensus).</summary>
    WaitForAll,

    /// <summary>Resolve as soon as quorum + majority are met; unvoted members' tasks are closed.</summary>
    Quorum
}
