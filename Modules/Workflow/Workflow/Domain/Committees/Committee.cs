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
        MajorityType majorityType)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(quorumValue, 0, nameof(quorumValue));

        var committee = new Committee
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Code = code,
            Description = description,
            IsActive = true,
            QuorumType = quorumType,
            QuorumValue = quorumValue,
            MajorityType = majorityType
        };
        return committee;
    }

    public void Update(string name, string? description, QuorumType quorumType, int quorumValue,
        MajorityType majorityType, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Description = description;
        QuorumType = quorumType;
        QuorumValue = quorumValue;
        MajorityType = majorityType;
        IsActive = isActive;
    }

    public CommitteeMember AddMember(string userId, string memberName, CommitteeMemberRole role)
    {
        var existing = _members.FirstOrDefault(m =>
            m.UserId == userId && m.IsActive);
        if (existing is not null)
            throw new InvalidOperationException($"User {userId} is already an active member of this committee");

        var member = CommitteeMember.Create(Id, userId, memberName, role);
        _members.Add(member);
        return member;
    }

    public void RemoveMember(Guid memberId)
    {
        var member = _members.FirstOrDefault(m => m.Id == memberId)
            ?? throw new NotFoundException($"CommitteeMember {memberId} not found");
        member.Deactivate();
    }

    public CommitteeThreshold AddThreshold(decimal? minValue, decimal? maxValue, int priority)
    {
        var threshold = CommitteeThreshold.Create(Id, minValue, maxValue, priority);
        _thresholds.Add(threshold);
        return threshold;
    }

    public CommitteeApprovalCondition AddCondition(
        ConditionType conditionType, string? roleRequired, int? minVotesRequired,
        int priority, string? description)
    {
        var condition = CommitteeApprovalCondition.Create(
            Id, conditionType, roleRequired, minVotesRequired, priority, description);
        _conditions.Add(condition);
        return condition;
    }

    public List<CommitteeMember> GetActiveMembers() =>
        _members.Where(m => m.IsActive).ToList();

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

    public bool HasMajority(int targetVoteCount, int totalVotes, int totalMembers)
    {
        return MajorityType switch
        {
            MajorityType.Simple => targetVoteCount > totalVotes / 2.0,
            MajorityType.TwoThirds => targetVoteCount >= Math.Ceiling(totalVotes * 2.0 / 3.0),
            MajorityType.Unanimous => targetVoteCount == totalMembers,
            _ => false
        };
    }
}

public enum QuorumType
{
    Fixed,
    Percentage
}

public enum MajorityType
{
    Simple,
    TwoThirds,
    Unanimous
}
