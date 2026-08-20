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

    private CommitteeApprovalCondition()
    {
    }

    internal static CommitteeApprovalCondition Create(
        Guid committeeId, ConditionType conditionType, string? roleRequired,
        int? minVotesRequired, int priority, string? description)
    {
        return new CommitteeApprovalCondition
        {
            // Assigned here, not left to EF: the command is ITransactionalCommand, so SaveChanges
            // runs after the handler returns and the endpoint would otherwise respond with an
            // all-zero Guid that the client cannot then PATCH or DELETE. There is no DB default on
            // this column, so an explicit value is simply used as-is.
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

    internal void Update(
        ConditionType conditionType, string? roleRequired,
        int? minVotesRequired, int priority, string? description, bool isActive)
    {
        ConditionType = conditionType;
        // Only the field the evaluator reads for this type is kept; the other is cleared so a
        // leftover value cannot resurface if the type is switched back later.
        RoleRequired = conditionType == ConditionType.RoleRequired ? roleRequired : null;
        MinVotesRequired = conditionType == ConditionType.MinVotes ? minVotesRequired : null;
        Priority = priority;
        Description = description;
        IsActive = isActive;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}

/// <summary>
/// Extra rules an approval round must satisfy on top of quorum and the majority rule. Evaluated by
/// <c>ApprovalActivity.CheckApprovalConditions</c>: every ACTIVE condition must pass or the round
/// does not complete.
/// </summary>
public enum ConditionType
{
    /// <summary>A member holding <see cref="CommitteeApprovalCondition.RoleRequired"/> must have cast the target vote.</summary>
    RoleRequired,

    /// <summary>At least <see cref="CommitteeApprovalCondition.MinVotesRequired"/> members must have cast the target vote.</summary>
    MinVotes
}