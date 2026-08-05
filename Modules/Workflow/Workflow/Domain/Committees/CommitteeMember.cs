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
    /// <summary>Risk representative. Retired — see <see cref="CommitteeMemberPositions.Selectable"/>.</summary>
    Risk,
    /// <summary>Appraisal representative. Retired — see <see cref="CommitteeMemberPositions.Selectable"/>.</summary>
    Appraisal,
    /// <summary>Credit representative. Retired — see <see cref="CommitteeMemberPositions.Selectable"/>.</summary>
    Credit,
    /// <summary>General committee member. Retired — see <see cref="CommitteeMemberPositions.Selectable"/>.</summary>
    Member
}

/// <summary>
/// Business rules over <see cref="CommitteeMemberPosition"/>.
/// </summary>
public static class CommitteeMemberPositions
{
    /// <summary>
    /// The positions a member may be assigned today, in display order.
    /// <see cref="CommitteeMemberPosition.Risk"/>, <see cref="CommitteeMemberPosition.Appraisal"/>,
    /// <see cref="CommitteeMemberPosition.Credit"/> and <see cref="CommitteeMemberPosition.Member"/>
    /// stay on the enum because Position is persisted as the enum NAME — existing committee members,
    /// meeting rosters and historical ApprovalVote.MemberRole rows still hold them and must keep
    /// materializing. They simply may no longer be chosen for a new or edited member.
    ///
    /// Declared before <see cref="Selectable"/>: static initializers run in textual order, so the
    /// set would be built from a null array if this came second.
    /// </summary>
    private static readonly CommitteeMemberPosition[] SelectableOrder =
    [
        CommitteeMemberPosition.Chairman,
        CommitteeMemberPosition.Director,
        CommitteeMemberPosition.Secretary,
        CommitteeMemberPosition.UW
    ];

    /// <summary>
    /// <see cref="SelectableOrder"/> as a set, for O(1) membership tests. A HashSet's iteration
    /// order is not contractually guaranteed, so anything user-facing is built from the array —
    /// never from this.
    /// </summary>
    public static readonly IReadOnlySet<CommitteeMemberPosition> Selectable = SelectableOrder.ToHashSet();

    /// <summary>
    /// Whether a member holding this position casts an approval vote once a meeting item is released.
    /// The Secretary convenes the meeting, releases its items and signs the minutes, but never votes —
    /// so they are excluded from the approver roster handed to the approval round.
    /// </summary>
    public static bool CanVote(CommitteeMemberPosition position) =>
        position != CommitteeMemberPosition.Secretary;

    /// <summary>Comma-separated <see cref="Selectable"/> names, for member validation messages.</summary>
    public static string SelectableNames => string.Join(", ", SelectableOrder);

    /// <summary>
    /// Comma-separated positions valid for a RoleRequired condition: selectable AND able to vote.
    /// Narrower than <see cref="SelectableNames"/>, which would advertise the Secretary as allowed
    /// on a call that refuses them.
    /// </summary>
    public static string RequirableNames => string.Join(", ", SelectableOrder.Where(CanVote));

    /// <summary>
    /// Parses a role string by NAME, without judging whether the position is still assignable.
    /// Name comparison rather than a bare <see cref="Enum.TryParse{T}(string, bool, out T)"/>:
    /// TryParse also accepts the numeric form ("3" -> UW), which would pass validation and then
    /// match no vote's role at runtime, since roles are compared as plain strings.
    /// </summary>
    public static bool TryParseName(string? role, out CommitteeMemberPosition position)
    {
        position = default;

        if (!Enum.GetNames<CommitteeMemberPosition>().Contains(role, StringComparer.OrdinalIgnoreCase))
            return false;

        position = Enum.Parse<CommitteeMemberPosition>(role!, ignoreCase: true);
        return true;
    }

    /// <summary>
    /// Parses a role string to a currently-assignable position, rejecting unknown names and retired
    /// ones alike. Use <see cref="TryParseName"/> instead where an existing retired value must be
    /// allowed to survive unchanged.
    /// </summary>
    public static bool TryParseSelectable(string? role, out CommitteeMemberPosition position) =>
        TryParseName(role, out position) && Selectable.Contains(position);
}
