namespace Workflow.Domain.Committees;

public class CommitteeMember : Entity<Guid>
{
    public Guid CommitteeId { get; private set; }
    public string UserId { get; private set; } = default!;
    public string MemberName { get; private set; } = default!;
    public CommitteeMemberPosition Position { get; private set; }
    public bool IsActive { get; private set; }

    private CommitteeMember() { }

    internal static CommitteeMember Create(Guid committeeId, string userId, string memberName,
        CommitteeMemberPosition position)
    {
        return new CommitteeMember
        {
            //Id = Guid.CreateVersion7(),
            CommitteeId = committeeId,
            UserId = userId,
            MemberName = memberName,
            Position = position,
            IsActive = true
        };
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public void UpdatePosition(CommitteeMemberPosition position)
    {
        Position = position;
    }
}

public enum CommitteeMemberPosition
{
    /// <summary>Committee Chairman.</summary>
    Chairman,
    /// <summary>Director-level member.</summary>
    Director,
    /// <summary>Committee Secretary.</summary>
    Secretary,
    /// <summary>Underwriter representative.</summary>
    UW,
    /// <summary>Risk representative.</summary>
    Risk,
    /// <summary>Appraisal representative.</summary>
    Appraisal,
    /// <summary>Credit representative.</summary>
    Credit,
    /// <summary>General committee member.</summary>
    Member
}
