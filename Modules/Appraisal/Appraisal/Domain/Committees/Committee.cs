namespace Appraisal.Domain.Committees;

/// <summary>
/// Committee Aggregate Root.
/// Manages committee members and approval conditions.
/// </summary>
public class Committee : Aggregate<Guid>
{
    private readonly List<CommitteeMember> _members = [];
    private readonly List<CommitteeApprovalCondition> _conditions = [];

    public IReadOnlyList<CommitteeMember> Members => _members.AsReadOnly();
    public IReadOnlyList<CommitteeApprovalCondition> Conditions => _conditions.AsReadOnly();

    // Core Properties
    public string CommitteeName { get; private set; } = null!;
    public string CommitteeCode { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Quorum Settings
    public string QuorumType { get; private set; } = null!; // Fixed, Percentage
    public int QuorumValue { get; private set; }
    public string MajorityType { get; private set; } = null!; // Simple, TwoThirds, Unanimous

    private Committee()
    {
    }

    public static Committee Create(
        string name,
        string code,
        string quorumType,
        int quorumValue,
        string majorityType)
    {
        return new Committee
        {
            Id = Guid.NewGuid(),
            CommitteeName = name,
            CommitteeCode = code,
            QuorumType = quorumType,
            QuorumValue = quorumValue,
            MajorityType = majorityType,
            IsActive = true
        };
    }

    public CommitteeMember AddMember(Guid userId, string name, string role)
    {
        if (_members.Any(m => m.UserId == userId && m.IsActive))
            throw new InvalidOperationException("User is already a member of this committee");

        var member = CommitteeMember.Create(Id, userId, name, role);
        _members.Add(member);
        return member;
    }

    public void RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId && m.IsActive);
        if (member == null)
            throw new InvalidOperationException("Member not found");

        member.Deactivate();
    }

    public CommitteeApprovalCondition AddCondition(
        string conditionType,
        string? roleRequired = null,
        int? minVotesRequired = null,
        string description = "")
    {
        var condition = CommitteeApprovalCondition.Create(
            Id, conditionType, roleRequired, minVotesRequired, description);
        _conditions.Add(condition);
        return condition;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public bool HasQuorum(int presentCount)
    {
        return QuorumType switch
        {
            "Fixed" => presentCount >= QuorumValue,
            "Percentage" => presentCount >= _members.Count(m => m.IsActive) * QuorumValue / 100,
            _ => false
        };
    }

    public bool HasMajority(int approveCount, int totalVotes)
    {
        return MajorityType switch
        {
            "Simple" => approveCount > totalVotes / 2,
            "TwoThirds" => approveCount >= totalVotes * 2 / 3,
            "Unanimous" => approveCount == totalVotes,
            _ => false
        };
    }
}