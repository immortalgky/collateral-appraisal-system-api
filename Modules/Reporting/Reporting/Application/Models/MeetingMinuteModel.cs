namespace Reporting.Application.Models;

/// <summary>
/// View-model for the "Meeting Minute" report (รายงานการประชุมคณะกรรมการ).
/// FSD §2.1.9.
/// Keyed by MeetingId (not AppraisalId).
/// </summary>
public sealed class MeetingMinuteModel
{
    // ── Meeting header ────────────────────────────────────────────────────────
    public string? MeetingNo { get; init; }
    public DateTime? StartAt { get; init; }
    public DateTime? EndAt { get; init; }
    public string? Location { get; init; }
    public DateTime? MinuteDate { get; init; }

    // ── Committee members present (ordered: Chairman first, then Directors, Secretary last) ──
    public IReadOnlyList<MeetingMemberRow> Members { get; init; } = Array.Empty<MeetingMemberRow>();

    // ── Staff presenters — DEFERRED: no source in current schema.
    //    Rendered as an empty placeholder table in the template.
    // public IReadOnlyList<PresenterRow> Presenters { get; init; } = ...;

    // ── Agenda groups (same grouping logic as invitation) ─────────────────────
    public IReadOnlyList<MeetingAgendaGroup> Agendas { get; init; } = Array.Empty<MeetingAgendaGroup>();

    // ── Per-committee-member opinion/signature block ──────────────────────────
    public IReadOnlyList<CommitteeOpinionRow> CommitteeOpinions { get; init; }
        = Array.Empty<CommitteeOpinionRow>();

    // ── No attachment slots — this report is self-contained ───────────────────
    public IReadOnlyDictionary<string, IReadOnlyList<Guid>> AttachmentsBySlot { get; init; }
        = new Dictionary<string, IReadOnlyList<Guid>>();
}

/// <summary>
/// Committee member opinion row used in the minute sign-off block.
/// MemberName + PositionThai are from MeetingMembers.
/// Opinion text comes from appraisal.CommitteeVotes.Comments — deferred to "" when absent.
/// </summary>
public sealed class CommitteeOpinionRow
{
    public string MemberName { get; init; } = string.Empty;
    public string PositionThai { get; init; } = string.Empty;

    /// <summary>
    /// Vote label Thai text ("เห็นด้วย" / "ไม่เห็นด้วย" / "รอพิจารณา" / "ส่งกลับ").
    /// Empty when no vote has been recorded yet.
    /// </summary>
    public string VoteLabel { get; init; } = string.Empty;

    /// <summary>
    /// Opinion/comments free text from CommitteeVotes.Comments.
    /// Deferred: empty string when no comments are present.
    /// </summary>
    public string Opinion { get; init; } = string.Empty;
}
