namespace Appraisal.Domain.Committees;

/// <summary>
/// Committee member entity.
/// </summary>
public class CommitteeMember : Entity<Guid>
{
    public Guid CommitteeId { get; private set; }
    public Guid UserId { get; private set; }
    public string MemberName { get; private set; } = null!;
    public string Role { get; private set; } = null!; // UW, Risk, Appraisal, Credit, Chairman
    public bool IsActive { get; private set; } = true;

    private CommitteeMember()
    {
    }

    public static CommitteeMember Create(
        Guid committeeId,
        Guid userId,
        string name,
        string role)
    {
        return new CommitteeMember
        {
            Id = Guid.CreateVersion7(),
            CommitteeId = committeeId,
            UserId = userId,
            MemberName = name,
            Role = role,
            IsActive = true
        };
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void UpdateRole(string role)
    {
        Role = role;
    }
}