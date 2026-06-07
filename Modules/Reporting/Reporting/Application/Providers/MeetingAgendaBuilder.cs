using System.Globalization;
using Reporting.Application.Models;

namespace Reporting.Application.Providers;

/// <summary>
/// Derives the FSD §2.1.8 วาระ 1–9 agenda groups from raw meeting data.
///
/// Grouping rules (Decision items only; Acknowledgement items are excluded):
///   วาระ 3 = New,  FacilityLimit &gt; 30,000,000
///   วาระ 4 = ReAppraisal, &gt; 30M
///   วาระ 5 = &gt; 30M and not New/ReAppraisal/Block (Construction/Progressive + catch-all)
///   วาระ 6 = Block/Project types (any FacilityLimit)
///   วาระ 7 = group 2: 10M &lt; FacilityLimit &lt;= 30M, non-Block
///   วาระ 8 = FacilityLimit &lt;= 10M, non-Block
///
/// Text-only agendas (no item list):
///   วาระ 1 = รับรองรายงานการประชุมครั้งที่ {previousMeetingNo} + AgendaCertifyMinutes
///   วาระ 2 = AgendaChairmanInformed
///   วาระ 9 = AgendaOthers (only if non-empty)
///
/// Only agendas that have at least one item (or a non-empty body text) are included.
/// </summary>
internal static class MeetingAgendaBuilder
{
    private const decimal Threshold30M = 30_000_000m;
    private const decimal Threshold10M = 10_000_000m;

    public static IReadOnlyList<MeetingAgendaGroup> Build(
        IReadOnlyList<MeetingItemFlat> items,
        string? previousMeetingNo,
        string? agendaCertifyMinutes,
        string? agendaChairmanInformed,
        string? agendaOthers)
    {
        var groups = new List<MeetingAgendaGroup>();

        // ── วาระ 1: รับรองรายงานการประชุม (certify previous minutes) ─────────
        var w1Body = BuildCertifyBody(previousMeetingNo, agendaCertifyMinutes);
        if (!string.IsNullOrWhiteSpace(w1Body))
        {
            groups.Add(new MeetingAgendaGroup
            {
                Number = 1,
                Title = "รับรองรายงานการประชุม",
                BodyText = w1Body
            });
        }

        // ── วาระ 2: แจ้งเพื่อทราบ (chairman informed) ───────────────────────
        if (!string.IsNullOrWhiteSpace(agendaChairmanInformed))
        {
            groups.Add(new MeetingAgendaGroup
            {
                Number = 2,
                Title = "แจ้งเพื่อทราบ",
                BodyText = agendaChairmanInformed
            });
        }

        // Decision items only
        var decisionItems = items.Where(i =>
            string.Equals(i.Kind, "Decision", StringComparison.OrdinalIgnoreCase)).ToList();

        // ── วาระ 3: ขออนุมัติราคา New > 30M ─────────────────────────────────
        var w3 = decisionItems
            .Where(i => IsNew(i) && i.FacilityLimit > Threshold30M)
            .Select(ToRow)
            .ToList();
        if (w3.Count > 0)
            groups.Add(MakeGroup(3, "ขออนุมัติราคาประเมินรายใหม่ (วงเงินเกิน 30 ล้านบาท)", w3));

        // ── วาระ 4: ขออนุมัติราคา ReAppraisal > 30M ──────────────────────────
        var w4 = decisionItems
            .Where(i => IsReAppraisal(i) && i.FacilityLimit > Threshold30M)
            .Select(ToRow)
            .ToList();
        if (w4.Count > 0)
            groups.Add(MakeGroup(4, "ขออนุมัติราคาประเมินซ้ำ (วงเงินเกิน 30 ล้านบาท)", w4));

        // ── วาระ 5: Construction/Progressive > 30M, AND the >30M catch-all ────────
        // Any decision item over 30M that is NOT New/ReAppraisal/Block lands here, so
        // an unhandled AppraisalType (e.g. PreAppraisal) is never silently dropped from
        // the committee agenda (the 10M/≤10M buckets only catch ≤30M items).
        var w5 = decisionItems
            .Where(i => i.FacilityLimit > Threshold30M
                        && !IsNew(i) && !IsReAppraisal(i) && !IsBlock(i))
            .Select(ToRow)
            .ToList();
        if (w5.Count > 0)
            groups.Add(MakeGroup(5, "ขออนุมัติราคาประเมินระหว่างก่อสร้าง (วงเงินเกิน 30 ล้านบาท)", w5));

        // ── วาระ 6: Block/Project ─────────────────────────────────────────────
        var w6 = decisionItems
            .Where(i => IsBlock(i))
            .Select(ToRow)
            .ToList();
        if (w6.Count > 0)
            groups.Add(MakeGroup(6, "ขออนุมัติราคาประเมินโครงการ", w6));

        // ── วาระ 7: กลุ่ม 2 (10M < limit <= 30M; excludes block) ─────────────
        var w7 = decisionItems
            .Where(i => !IsBlock(i) && i.FacilityLimit > Threshold10M && i.FacilityLimit <= Threshold30M)
            .Select(ToRow)
            .ToList();
        if (w7.Count > 0)
            groups.Add(MakeGroup(7, "ขออนุมัติราคาประเมิน (วงเงิน 10–30 ล้านบาท)", w7));

        // ── วาระ 8: กลุ่ม 3 (<= 10M; excludes block) ────────────────────────
        var w8 = decisionItems
            .Where(i => !IsBlock(i) && i.FacilityLimit <= Threshold10M)
            .Select(ToRow)
            .ToList();
        if (w8.Count > 0)
            groups.Add(MakeGroup(8, "ขออนุมัติราคาประเมิน (วงเงินไม่เกิน 10 ล้านบาท)", w8));

        // ── วาระ 9: วาระอื่น ๆ (others free text) ────────────────────────────
        if (!string.IsNullOrWhiteSpace(agendaOthers))
        {
            groups.Add(new MeetingAgendaGroup
            {
                Number = 9,
                Title = "วาระอื่น ๆ",
                BodyText = agendaOthers
            });
        }

        return groups.AsReadOnly();
    }

    // ── AppraisalType classification helpers ──────────────────────────────────

    private static bool IsNew(MeetingItemFlat i) =>
        string.Equals(i.AppraisalType, "New", StringComparison.OrdinalIgnoreCase);

    private static bool IsReAppraisal(MeetingItemFlat i) =>
        string.Equals(i.AppraisalType, "ReAppraisal", StringComparison.OrdinalIgnoreCase);

    private static bool IsConstruction(MeetingItemFlat i) =>
        string.Equals(i.AppraisalType, "Construction", StringComparison.OrdinalIgnoreCase)
        || string.Equals(i.AppraisalType, "Progressive", StringComparison.OrdinalIgnoreCase);

    private static bool IsBlock(MeetingItemFlat i) =>
        string.Equals(i.AppraisalType, "Block", StringComparison.OrdinalIgnoreCase)
        || string.Equals(i.AppraisalType, "Project", StringComparison.OrdinalIgnoreCase)
        || string.Equals(i.AppraisalType, "BlockCondo", StringComparison.OrdinalIgnoreCase)
        || string.Equals(i.AppraisalType, "BlockLandBuilding", StringComparison.OrdinalIgnoreCase);

    // ── Row builder ───────────────────────────────────────────────────────────

    private static MeetingAgendaItemRow ToRow(MeetingItemFlat i)
    {
        // When IsPriceVerified is explicitly false, show ไม่รับรองราคา instead of the value.
        var valueText = i.IsPriceVerified == false
            ? "ไม่รับรองราคา"
            : i.AppraisedValue.HasValue
                ? i.AppraisedValue.Value.ToString("N2", CultureInfo.InvariantCulture)
                : string.Empty;

        return new MeetingAgendaItemRow
        {
            CustomerName = i.CustomerName ?? string.Empty,
            ValueText = valueText
        };
    }

    private static MeetingAgendaGroup MakeGroup(int number, string title, List<MeetingAgendaItemRow> rows) =>
        new()
        {
            Number = number,
            Title = title,
            CountText = $"จำนวน {rows.Count} ราย",
            Items = rows.AsReadOnly()
        };

    private static string BuildCertifyBody(string? previousMeetingNo, string? agendaCertifyMinutes)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(previousMeetingNo))
            parts.Add($"รับรองรายงานการประชุมครั้งที่ {previousMeetingNo}");
        if (!string.IsNullOrWhiteSpace(agendaCertifyMinutes))
            parts.Add(agendaCertifyMinutes.Trim());
        return string.Join(" — ", parts);
    }
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
    public decimal FacilityLimit { get; init; }
    public string? CustomerName { get; init; }
    public decimal? AppraisedValue { get; init; }

    /// <summary>
    /// Null or true = show value; false = show "ไม่รับรองราคา".
    /// Sourced from appraisal.AppraisalDecisions.IsPriceVerified.
    /// </summary>
    public bool? IsPriceVerified { get; init; }
}
