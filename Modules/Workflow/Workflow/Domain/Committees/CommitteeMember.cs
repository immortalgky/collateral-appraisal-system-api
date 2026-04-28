namespace Workflow.Domain.Committees;

public class CommitteeMember : Entity<Guid>
{
    public Guid CommitteeId { get; private set; }
    public string UserId { get; private set; } = default!;
    public string MemberName { get; private set; } = default!;
    public CommitteeMemberPosition Position { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>
    /// Controls which meetings this member attends based on the meeting sequence parity.
    /// <see cref="CommitteeAttendance.Always"/> = every meeting (default).
    /// <see cref="CommitteeAttendance.Odd"/> = odd-numbered meetings only (seq % 2 == 1).
    /// <see cref="CommitteeAttendance.Even"/> = even-numbered meetings only (seq % 2 == 0).
    /// </summary>
    public CommitteeAttendance Attendance { get; private set; } = CommitteeAttendance.Always;

    private CommitteeMember() { }

    internal static CommitteeMember Create(Guid committeeId, string userId, string memberName,
        CommitteeMemberPosition position,
        CommitteeAttendance attendance = CommitteeAttendance.Always)
    {
        return new CommitteeMember
        {
            //Id = Guid.CreateVersion7(),
            CommitteeId = committeeId,
            UserId = userId,
            MemberName = memberName,
            Position = position,
            IsActive = true,
            Attendance = attendance
        };
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public void UpdatePosition(CommitteeMemberPosition position)
    {
        Position = position;
    }

    public void UpdateAttendance(CommitteeAttendance attendance)
    {
        Attendance = attendance;
    }
}

/// <summary>
/// Parity-based attendance rule for a committee member.
/// Keyed on <see cref="Meeting.MeetingNoSeq"/> at snapshot time.
/// </summary>
public enum CommitteeAttendance
{
    /// <summary>Attend every meeting regardless of sequence number.</summary>
    Always,
    /// <summary>Attend only when the meeting sequence number is odd (seq % 2 == 1).</summary>
    Odd,
    /// <summary>Attend only when the meeting sequence number is even (seq % 2 == 0).</summary>
    Even
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
