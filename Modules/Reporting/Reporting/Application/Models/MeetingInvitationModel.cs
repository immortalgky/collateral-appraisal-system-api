namespace Reporting.Application.Models;

/// <summary>
/// View-model for the "Meeting Invitation" report (บันทึกภายใน / เชิญประชุมคณะกรรมการ).
/// FSD §2.1.8.
/// Keyed by MeetingId (not AppraisalId).
/// </summary>
public sealed class MeetingInvitationModel
{
    // ── Meeting header ────────────────────────────────────────────────────────
    public string? MeetingNo { get; init; }
    public DateTime? StartAt { get; init; }
    public DateTime? EndAt { get; init; }
    public string? Location { get; init; }
    public string? FromText { get; init; }
    public string? ToText { get; init; }
    public DateTime? InvitationDate { get; init; }

    // ── Agenda agenda items (FSD วาระ 1–9) ───────────────────────────────────
    public IReadOnlyList<MeetingAgendaGroup> Agendas { get; init; } = Array.Empty<MeetingAgendaGroup>();

    // ── Committee member list (for sign-off block) ────────────────────────────
    public IReadOnlyList<MeetingMemberRow> Members { get; init; } = Array.Empty<MeetingMemberRow>();

    // ── Secretary (single member whose Position = Secretary) ─────────────────
    public string? SecretaryName { get; init; }

    // ── No attachment slots — this report is self-contained ───────────────────
    public IReadOnlyDictionary<string, IReadOnlyList<Guid>> AttachmentsBySlot { get; init; }
        = new Dictionary<string, IReadOnlyList<Guid>>();
}

/// <summary>
/// One วาระ (agenda item group) in the meeting invitation.
/// Only agendas with at least one item are included.
/// </summary>
public sealed class MeetingAgendaGroup
{
    /// <summary>Agenda number (1–9).</summary>
    public int Number { get; init; }

    /// <summary>Thai title for this agenda วาระ.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Human-readable "จำนวน N ราย" count text (empty for text-only agendas).</summary>
    public string CountText { get; init; } = string.Empty;

    /// <summary>Free-form body text (used for วาระ 1/2/9 which are text-based).</summary>
    public string? BodyText { get; init; }

    /// <summary>Line items within this agenda (numbered list).</summary>
    public IReadOnlyList<MeetingAgendaItemRow> Items { get; init; } = Array.Empty<MeetingAgendaItemRow>();
}

/// <summary>One appraisal row within an agenda group.</summary>
public sealed class MeetingAgendaItemRow
{
    public string CustomerName { get; init; } = string.Empty;

    /// <summary>
    /// Formatted appraisal value text, e.g. "12,500,000.00" or "ไม่รับรองราคา"
    /// when IsPriceVerified is false/null.
    /// </summary>
    public string ValueText { get; init; } = string.Empty;
}

/// <summary>One committee member row in the sign-off block.</summary>
public sealed class MeetingMemberRow
{
    public string MemberName { get; init; } = string.Empty;
    /// <summary>Thai position label (ประธาน / กรรมการ / เลขานุการฯ / …).</summary>
    public string PositionThai { get; init; } = string.Empty;
}
