using Shared.DDD;
using Workflow.Domain.Committees;

namespace Workflow.Meetings.Domain;

/// <summary>
/// A snapshot of a committee member attached to a specific meeting.
/// Created either by copying an active <see cref="CommitteeMember"/> at meeting creation
/// or by manually adding a user afterwards.
/// </summary>
public class MeetingMember : Entity<Guid>
{
    public Guid MeetingId { get; private set; }
    public string UserId { get; private set; } = default!;
    public string MemberName { get; private set; } = default!;
    public CommitteeMemberPosition Position { get; private set; }

    /// <summary>
    /// The source <see cref="CommitteeMember"/> ID when this member was created via
    /// <see cref="CreateSnapshot"/>; null for manually-added members.
    /// </summary>
    public Guid? SourceCommitteeMemberId { get; private set; }

    public DateTime AddedAt { get; private set; }

    private MeetingMember()
    {
    }

    /// <summary>
    /// Creates a snapshot of an existing active committee member for a meeting.
    /// </summary>
    public static MeetingMember CreateSnapshot(Guid meetingId, CommitteeMember committeeMember)
    {
        ArgumentNullException.ThrowIfNull(committeeMember);

        return new MeetingMember
        {
            //Id = Guid.CreateVersion7(),
            MeetingId = meetingId,
            UserId = committeeMember.UserId,
            MemberName = committeeMember.MemberName,
            Position = committeeMember.Position,
            SourceCommitteeMemberId = committeeMember.Id,
            AddedAt = DateTime.Now
        };
    }

    /// <summary>
    /// Manually adds a user as a meeting member (not necessarily from the committee roster).
    /// </summary>
    public static MeetingMember CreateManual(
        Guid meetingId,
        string userId,
        string memberName,
        CommitteeMemberPosition position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);

        return new MeetingMember
        {
            Id = Guid.CreateVersion7(),
            MeetingId = meetingId,
            UserId = userId,
            MemberName = memberName.Trim(),
            Position = position,
            SourceCommitteeMemberId = null,
            AddedAt = DateTime.Now
        };
    }

    /// <summary>Updates the position of this meeting member.</summary>
    public void UpdatePosition(CommitteeMemberPosition position)
    {
        Position = position;
    }
}