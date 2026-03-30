namespace Workflow.Domain.Committees;

public class CommitteeApprovalCondition : Entity<Guid>
{
    public Guid CommitteeId { get; private set; }
    public ConditionType ConditionType { get; private set; }
    public string? RoleRequired { get; private set; }
    public int? MinVotesRequired { get; private set; }
    public int Priority { get; private set; }
    public bool IsActive { get; private set; }
    public string? Description { get; private set; }

    private CommitteeApprovalCondition() { }

    internal static CommitteeApprovalCondition Create(
        Guid committeeId, ConditionType conditionType, string? roleRequired,
        int? minVotesRequired, int priority, string? description)
    {
        return new CommitteeApprovalCondition
        {
            Id = Guid.CreateVersion7(),
            CommitteeId = committeeId,
            ConditionType = conditionType,
            RoleRequired = roleRequired,
            MinVotesRequired = minVotesRequired,
            Priority = priority,
            IsActive = true,
            Description = description
        };
    }

    public void Deactivate() => IsActive = false;
}

public enum ConditionType
{
    RoleRequired,
    MinVotes
}
