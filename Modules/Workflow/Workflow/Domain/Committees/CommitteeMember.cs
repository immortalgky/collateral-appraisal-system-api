namespace Workflow.Domain.Committees;

public class CommitteeMember : Entity<Guid>
{
    public Guid CommitteeId { get; private set; }
    public string UserId { get; private set; } = default!;
    public string MemberName { get; private set; } = default!;
    public CommitteeMemberRole Role { get; private set; }
    public bool IsActive { get; private set; }

    private CommitteeMember() { }

    internal static CommitteeMember Create(Guid committeeId, string userId, string memberName,
        CommitteeMemberRole role)
    {
        return new CommitteeMember
        {
            //Id = Guid.CreateVersion7(),
            CommitteeId = committeeId,
            UserId = userId,
            MemberName = memberName,
            Role = role,
            IsActive = true
        };
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public void UpdateRole(CommitteeMemberRole role)
    {
        Role = role;
    }
}

public enum CommitteeMemberRole
{
    Chairman,
    UW,
    Risk,
    Appraisal,
    Credit,
    Member
}
