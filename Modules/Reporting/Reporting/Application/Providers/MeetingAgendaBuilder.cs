using System.Globalization;
using Reporting.Application.Models;

namespace Reporting.Application.Providers;

/// <summary>
/// Derives the FSD §2.1.8 วาระ 1–9 agenda groups from raw meeting data. The grouping mirrors
/// the meeting-detail screen (<c>GetMeetingDetailQueryHandler</c>): Decision items split by
/// <see cref="MeetingItemFlat.AppraisalType"/>, Acknowledgement items split by
/// <see cref="MeetingItemFlat.AcknowledgementGroup"/>. There is NO facility-amount bucketing.
///
///   วาระ 3 = Decision · New                 (NEW APPRAISALS)
///   วาระ 4 = Decision · ReAppraisal         (RE-APPRAISALS)
///   วาระ 5 = Decision · Progressive         (PROGRESSIVE APPRAISALS)
///   วาระ 6 = Decision · PreAppraisal        (PRE-APPRAISALS / block-MF)
///   วาระ 7 = Acknowledgement · group "2"    (ACKNOWLEDGE FOR URGENT APPROVAL / GROUP 2)
///   วาระ 8 = Acknowledgement · other groups (ACKNOWLEDGE / GROUP 1)
///
/// Text-only agendas (no item list):
///   วาระ 1 = รับรองรายงานการประชุมครั้งที่ {previousMeetingNo} + AgendaCertifyMinutes
///   วาระ 2 = AgendaChairmanInformed
///   วาระ 9 = AgendaOthers
///
/// All 9 วาระ are always emitted (fixed form), even when a วาระ carries no items/data.
/// </summary>
internal static class MeetingAgendaBuilder
{
    // Acknowledgement group whose items go to วาระ 7 (urgent approval / Group 2). Per
    // appsettings Workflow:AcknowledgementGroupByCommitteeCode, COMMITTEE → "2". Everything
    // else (e.g. SUB_COMMITTEE → "1") falls to วาระ 8 so no acknowledgement item is dropped.
    private const string UrgentApprovalGroup = "2";

    public static IReadOnlyList<MeetingAgendaGroup> Build(
        IReadOnlyList<MeetingItemFlat> items,
        string? previousMeetingNo,
        string? agendaCertifyMinutes,
        string? agendaChairmanInformed,
        string? agendaOthers)
    {
        var byWara = EnumerateAgendaItems(items)
            .GroupBy(x => x.Wara)
            .ToDictionary(g => g.Key, g => g.Select(x => ToRow(x.Item)).ToList());

        List<MeetingAgendaItemRow> Rows(int wara) =>
            byWara.TryGetValue(wara, out var rows) ? rows : new List<MeetingAgendaItemRow>();

        // FSD §2.1.8 — all 9 วาระ are a fixed form and always rendered, even when a วาระ carries
        // no items/data. Decision วาระ (3–8) always show their "จำนวน N ราย" count (N may be 0).
        return new List<MeetingAgendaGroup>
        {
            TextGroup(1, BuildCertifyTitle(previousMeetingNo), agendaCertifyMinutes),
            TextGroup(2, "ประธานแจ้งเพื่อทราบ", agendaChairmanInformed),
            DecisionGroup(3, "กำหนดราคาประเมินของวงเงินสินเชื่อใหม่", Rows(3)),
            DecisionGroup(4, "ขอทบทวนราคาประเมิน", Rows(4)),
            DecisionGroup(5, "การตรวจงวดงานก่อสร้าง", Rows(5)),
            DecisionGroup(6, "งานประเมินเพื่อสนับสนุนรายย่อยภายในโครงการ (MF)", Rows(6)),
            DecisionGroup(7, "อนุมัติเร่งด่วน", Rows(7)),
            DecisionGroup(8, "แจ้งเพื่อทราบ", Rows(8)),
            TextGroup(9, "อื่นๆ", agendaOthers),
        }.AsReadOnly();
    }

    /// <summary>
    /// FSD §2.1.9 fields 4.1–4.3: distinct appraisal staff (presenters) across all decision/ack
    /// agenda items, each with their position and the "{วาระ}.{seq}" references of the items they
    /// presented (e.g. a staff on วาระ 8 item 1 → "8.1"; multiple → "8.1, 8.2"). Minute only.
    /// </summary>
    public static IReadOnlyList<MeetingPresenterRow> BuildPresenters(IReadOnlyList<MeetingItemFlat> items)
    {
        var byStaff = new Dictionary<string, (string Position, List<string> Refs)>();
        var order = new List<string>();

        foreach (var (wara, seq, item) in EnumerateAgendaItems(items))
        {
            var name = item.AppraisalStaff?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (!byStaff.TryGetValue(name, out var entry))
            {
                entry = (item.StaffPosition?.Trim() ?? string.Empty, new List<string>());
                byStaff[name] = entry;
                order.Add(name);
            }
            entry.Refs.Add($"{wara}.{seq}");
        }

        return order
            .Select(name => new MeetingPresenterRow
            {
                PresenterName = name,
                Position = byStaff[name].Position,
                AgendaRefs = string.Join(", ", byStaff[name].Refs)
            })
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Yields every decision/ack agenda item with its วาระ number (3–8) and 1-based sequence
    /// within that วาระ — the single source of truth shared by <see cref="Build"/> (item rows)
    /// and <see cref="BuildPresenters"/> (presenter references), so the two never disagree.
    /// </summary>
    private static IEnumerable<(int Wara, int Seq, MeetingItemFlat Item)> EnumerateAgendaItems(
        IReadOnlyList<MeetingItemFlat> items)
    {
        var decisionItems = items.Where(i =>
            string.Equals(i.Kind, "Decision", StringComparison.OrdinalIgnoreCase)).ToList();
        var ackItems = items.Where(i =>
            string.Equals(i.Kind, "Acknowledgement", StringComparison.OrdinalIgnoreCase)).ToList();

        var buckets = new (int Wara, List<MeetingItemFlat> Items)[]
        {
            (3, decisionItems.Where(IsNew).ToList()),
            (4, decisionItems.Where(IsReAppraisal).ToList()),
            (5, decisionItems.Where(IsProgressive).ToList()),
            (6, decisionItems.Where(IsBlock).ToList()),
            (7, ackItems.Where(IsUrgentGroup).ToList()),
            (8, ackItems.Where(i => !IsUrgentGroup(i)).ToList()),
        };

        foreach (var (wara, list) in buckets)
            for (var idx = 0; idx < list.Count; idx++)
                yield return (wara, idx + 1, list[idx]);
    }

    // ── AppraisalType classification helpers ──────────────────────────────────

    private static bool IsNew(MeetingItemFlat i) =>
        string.Equals(i.AppraisalType, "New", StringComparison.OrdinalIgnoreCase);

    private static bool IsReAppraisal(MeetingItemFlat i) =>
        string.Equals(i.AppraisalType, "ReAppraisal", StringComparison.OrdinalIgnoreCase);

    private static bool IsProgressive(MeetingItemFlat i) =>
        string.Equals(i.AppraisalType, "Progressive", StringComparison.OrdinalIgnoreCase);

    // Block/Project (MF — งานประเมินเพื่อสนับสนุนรายย่อยภายในโครงการ, FSD วาระ 6) is carried on
    // Appraisal.AppraisalType == "PreAppraisal".
    private static bool IsBlock(MeetingItemFlat i) =>
        string.Equals(i.AppraisalType, "PreAppraisal", StringComparison.OrdinalIgnoreCase);

    private static bool IsUrgentGroup(MeetingItemFlat i) =>
        string.Equals(i.AcknowledgementGroup, UrgentApprovalGroup, StringComparison.OrdinalIgnoreCase);

    // ── Row builder ───────────────────────────────────────────────────────────

    private static MeetingAgendaItemRow ToRow(MeetingItemFlat i)
    {
        // Block/Project items (วาระ 6) show the project name + "ดูตามสรุปราคา" wording instead of a
        // customer name and numeric value (FSD §2.1.8 #25 / §2.1.9 #24-25). The empty value lets the
        // invitation render the project name alone; the minute template supplies the FSD value wording.
        if (IsBlock(i))
        {
            return new MeetingAgendaItemRow
            {
                CustomerName = string.IsNullOrWhiteSpace(i.ProjectName)
                    ? JoinThaiNames(i.CustomerName)
                    : i.ProjectName!,
                ValueText = string.Empty
            };
        }

        // When IsPriceVerified is explicitly false, show ไม่รับรองราคา instead of the value.
        var valueText = i.IsPriceVerified == false
            ? "ไม่รับรองราคา"
            : i.AppraisedValue.HasValue
                ? i.AppraisedValue.Value.ToString("N2", CultureInfo.InvariantCulture)
                : string.Empty;

        return new MeetingAgendaItemRow
        {
            CustomerName = JoinThaiNames(i.CustomerName),
            ValueText = valueText
        };
    }

    /// <summary>
    /// Joins a "||"-delimited customer-name list (from the provider's STRING_AGG) into the
    /// FSD display form: names separated by ", " with "และ" before the last
    /// (e.g. "ก, ข และ ค"). Single name passes through unchanged.
    /// </summary>
    internal static string JoinThaiNames(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var names = raw
            .Split("||", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return names.Length switch
        {
            0 => string.Empty,
            1 => names[0],
            _ => string.Join(", ", names[..^1]) + " และ " + names[^1]
        };
    }

    /// <summary>A decision วาระ (3–8): always carries a "จำนวน N ราย" count, items may be empty.</summary>
    private static MeetingAgendaGroup DecisionGroup(int number, string title, List<MeetingAgendaItemRow> rows) =>
        new()
        {
            Number = number,
            Title = title,
            CountText = $"จำนวน {rows.Count} ราย",
            Items = rows.AsReadOnly()
        };

    /// <summary>A text วาระ (1/2/9): heading always shown; body text optional.</summary>
    private static MeetingAgendaGroup TextGroup(int number, string title, string? bodyText) =>
        new()
        {
            Number = number,
            Title = title,
            BodyText = string.IsNullOrWhiteSpace(bodyText) ? null : bodyText.Trim()
        };

    private static string BuildCertifyTitle(string? previousMeetingNo) =>
        string.IsNullOrWhiteSpace(previousMeetingNo)
            ? "รับรองรายงานการประชุม"
            : $"รับรองรายงานการประชุมครั้งที่ {previousMeetingNo}";
}

/// <summary>
/// Flat Dapper row used as input to <see cref="MeetingAgendaBuilder.Build"/>.
/// Shared between MeetingInvitationDataProvider and MeetingMinuteDataProvider.
/// </summary>
internal sealed class MeetingItemFlat
{
    public Guid AppraisalId { get; init; }
    public string? Kind { get; init; }
    public string? AppraisalType { get; init; }

    /// <summary>Acknowledgement group key ("1"/"2") for ack items; null for decision items.</summary>
    public string? AcknowledgementGroup { get; init; }

    public decimal FacilityLimit { get; init; }
    public string? CustomerName { get; init; }

    /// <summary>Appraisal staff (presenter) display name; FSD §2.1.9 field 4.1. Minute only.</summary>
    public string? AppraisalStaff { get; init; }

    /// <summary>Appraisal staff position/title (auth.AspNetUsers.Position); FSD field 4.2. Minute only.</summary>
    public string? StaffPosition { get; init; }

    /// <summary>Project name (appraisal.Projects) for block items; null for non-block.</summary>
    public string? ProjectName { get; init; }

    public decimal? AppraisedValue { get; init; }

    /// <summary>
    /// Null or true = show value; false = show "ไม่รับรองราคา".
    /// Sourced from appraisal.AppraisalDecisions.IsPriceVerified.
    /// </summary>
    public bool? IsPriceVerified { get; init; }
}
