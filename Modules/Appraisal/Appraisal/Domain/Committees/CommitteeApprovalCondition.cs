namespace Appraisal.Domain.Committees;

/// <summary>
/// Defines conditions required for committee approval.
/// </summary>
public class CommitteeApprovalCondition : Entity<Guid>
{
    public Guid CommitteeId { get; private set; }
    public string ConditionType { get; private set; } = null!; // RoleRequired, MinVotes
    public string? RoleRequired { get; private set; } // "UW", "Risk"
    public int? MinVotesRequired { get; private set; }
    public string Description { get; private set; } = null!;
    public int Priority { get; private set; }
    public bool IsActive { get; private set; } = true;

    private CommitteeApprovalCondition()
    {
    }

    public static CommitteeApprovalCondition Create(
        Guid committeeId,
        string conditionType,
        string? roleRequired,
        int? minVotesRequired,
        string description)
    {
        return new CommitteeApprovalCondition
        {
            Id = Guid.NewGuid(),
            CommitteeId = committeeId,
            ConditionType = conditionType,
            RoleRequired = roleRequired,
            MinVotesRequired = minVotesRequired,
            Description = description,
            Priority = 0,
            IsActive = true
        };
    }

    public void SetPriority(int priority)
    {
        Priority = priority;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}