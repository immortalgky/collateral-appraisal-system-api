using Reporting.Application.Models;

namespace Reporting.Application.Providers;

/// <summary>
/// Section 5.1 วิธีการประเมินมูลค่า as prose. One builder over the analysed approaches (<see cref="ApproachValueRow"/>: the used ones, then the
/// reference ones, each with the methods that gave its value) and a phrase table keyed by approach / method type, so the text can never name a method
/// the value did not come from. A single group reads "ในการประเมินมูลค่าครั้งนี้ ผู้ประเมินใช้…"; with several groups each group's first paragraph starts
/// with "กลุ่มที่ N "; its reference sentence follows directly under it with no prefix (a group with only reference approaches has that
/// sentence as its first, so it carries the prefix).
/// </summary>
internal static class PricingNarrative
{
    // ── Phrase table ─────────────────────────────────────────────────────────────────────
    // Approach: the Thai name with its English name in brackets. The short form (no brackets) is used when one sentence names several approaches.
    private static readonly IReadOnlyDictionary<string, (string Thai, string English)> Approaches =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Market"]   = ("วิธีเปรียบเทียบตลาด", "Market Approach"),
            ["Cost"]     = ("วิธีต้นทุน", "Cost Approach"),
            ["Income"]   = ("วิธีรายได้", "Income Approach"),
            ["Residual"] = ("วิธีสมมติฐาน", "Residual Approach"),
        };

    // Method: how the part is valued ("ประเมินมูลค่า{part}ด้วย{method}").
    private static readonly IReadOnlyDictionary<string, string> Methods =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["WQS"]              = "วิธีเปรียบเทียบตลาดแบบถ่วงน้ำหนักคะแนน (WQS)",
            ["SaleGrid"]         = "วิธีเปรียบเทียบตลาดแบบปรับปรุงราคา (Sale Adjustment Grid)",
            ["DirectComparison"] = "วิธีเปรียบเทียบตลาดโดยตรง (Direct Comparison)",
            ["BuildingCost"]     = "วิธีต้นทุนทดแทนหักค่าเสื่อมราคา",
            ["MachineryCost"]    = "วิธีต้นทุนทดแทนหักค่าเสื่อมราคา",
            ["ProfitRent"]       = "วิธีกำไรส่วนเกินจากค่าเช่า (Profit Rent)",
            ["Leasehold"]        = "วิธีประเมินมูลค่าสิทธิการเช่า (Leasehold)",
            ["Income"]           = "วิธีวิเคราะห์รายได้ (Income)",
            ["Hypothesis"]       = "วิธีสมมติฐานการพัฒนา (Hypothesis)",
        };

    // Role: the part of the property a method valued, in the order the parts are listed.
    private static readonly string[] RoleOrder = ["land", "building", "landandbuilding", "machinery"];

    private static readonly IReadOnlyDictionary<string, string> Roles =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["land"]            = "ที่ดิน",
            ["building"]        = "สิ่งปลูกสร้าง",
            ["landandbuilding"] = "ที่ดินพร้อมสิ่งปลูกสร้าง",
            ["machinery"]       = "เครื่องจักร",
        };

    private const string WholeProperty = "ทรัพย์สิน";

    /// <summary>
    /// The approach(es) the valuation used, with their English names ("วิธีต้นทุน (Cost Approach) และวิธีเปรียบเทียบตลาด (Market Approach)"):
    /// the used rows (not the reference ones), each approach once, in first-seen order. Null when no approach was used.
    /// </summary>
    public static string? SelectedApproaches(IReadOnlyList<ApproachValueRow> rows)
    {
        var used = rows.Where(r => !r.IsReference && r.ApproachType is not null).Select(r => ApproachFull(r.ApproachType)).Distinct().ToList();
        return used.Count == 0 ? null : string.Join(" และ", used);
    }

    private static string ApproachFull(string? type) =>
        type is not null && Approaches.TryGetValue(type, out var a) ? $"{a.Thai} ({a.English})" : type ?? "";

    private static string ApproachShort(string? type) =>
        type is not null && Approaches.TryGetValue(type, out var a) ? a.Thai : type ?? "";

    private static string MethodPhrase(string methodType) => Methods.GetValueOrDefault(methodType, methodType);

    private static int RoleRank(string? role)
    {
        var i = role is null ? -1 : Array.IndexOf(RoleOrder, role.ToLowerInvariant());
        return i < 0 ? RoleOrder.Length : i;
    }

    /// <summary>"ประเมินมูลค่าที่ดินด้วยวิธี…" for each part of one approach, parts in role order, methods of one part joined with "และ".</summary>
    private static List<string> Parts(ApproachValueRow approach) =>
        approach.Methods
            .GroupBy(m => m.Role?.ToLowerInvariant() ?? string.Empty)
            .OrderBy(g => RoleRank(g.Key))
            .Select(g =>
            {
                var part = g.Key.Length > 0 && Roles.TryGetValue(g.Key, out var thai) ? thai : WholeProperty;
                var methods = g.Select(m => MethodPhrase(m.MethodType)).Distinct().ToList();
                return $"ประเมินมูลค่า{part}ด้วย{string.Join(" และ", methods)}";
            })
            .ToList();

    // ── Section 6 approach intro ───────────────────────────────────────────────────────────────

    /// <summary>"การประเมินมูลค่าทรัพย์สินโดยวิธีต้นทุน (Cost Approach)": the bold title line above an approach's intro paragraph in section 6.</summary>
    public static string IntroTitle(ApproachValueRow approach) => $"การประเมินมูลค่าทรัพย์สินโดย{ApproachFull(approach.ApproachType)}";

    private static List<string> MethodPhrases(IEnumerable<ApproachMethodRef> methods) =>
        methods.Select(m => MethodPhrase(m.MethodType)).Distinct().ToList();

    /// <summary>
    /// The intro paragraph of one used approach in section 6, from the same phrase tables as 5.1. A cost approach reads as the land's market value
    /// plus the building's depreciated construction cost (or the matching half alone); a market approach says what it compares with; any other
    /// approach names its methods. The sentence is cut short, never invented, when no method is known.
    /// </summary>
    public static string IntroText(ApproachValueRow approach)
    {
        var title = IntroTitle(approach);
        const string Tail = " โดยมีรายละเอียดดังนี้";

        if (string.Equals(approach.ApproachType, "Cost", StringComparison.OrdinalIgnoreCase))
        {
            var byRole = approach.Methods.GroupBy(m => m.Role?.ToLowerInvariant() ?? string.Empty).ToDictionary(g => g.Key, g => g.ToList());
            var onlyLandAndBuilding = byRole.Count > 0 && byRole.Keys.All(k => k is "land" or "building");
            if (onlyLandAndBuilding)
            {
                var halves = new List<string>();
                if (byRole.TryGetValue("land", out var land))
                    halves.Add($"มูลค่าตลาดของที่ดินด้วย{string.Join(" และ", MethodPhrases(land))}");
                if (byRole.ContainsKey("building"))
                    halves.Add("ต้นทุนค่าก่อสร้างตามสภาพปัจจุบันของสิ่งปลูกสร้างหลังหักค่าเสื่อมราคา");
                return $"{title} เป็นการพิจารณา{string.Join(" รวมกับ", halves)}{Tail}";
            }
        }
        else if (string.Equals(approach.ApproachType, "Market", StringComparison.OrdinalIgnoreCase))
        {
            var compare = $"{title} เป็นการเปรียบเทียบกับข้อมูลซื้อขายหรือเสนอขายทรัพย์สินที่คล้ายคลึงกัน";
            return approach.Methods.Count > 0
                ? $"{compare} ด้วย{string.Join(" และ", MethodPhrases(approach.Methods))}{Tail}"
                : $"{compare}{Tail}";
        }

        // Income / Residual / anything else, and a cost approach with parts other than land and building (machinery ...): name the methods.
        return approach.Methods.Count > 0
            ? $"{title} ด้วย{string.Join(" และ", MethodPhrases(approach.Methods))}{Tail}"
            : $"{title}{Tail}";
    }

    /// <summary>One group's paragraphs: how the value was reached, then (only when there are reference approaches) what was kept for reference.</summary>
    private static IEnumerable<string> ForGroup(IReadOnlyList<ApproachValueRow> rows, string? groupLead)
    {
        var used = rows.Where(r => !r.IsReference).ToList();
        var reference = rows.Where(r => r.IsReference).ToList();
        var lead = groupLead is null ? "ในการประเมินมูลค่าครั้งนี้ ผู้ประเมิน" : $"{groupLead}ผู้ประเมิน";

        if (used.Count > 0)
        {
            var sentence = $"{lead}ใช้{string.Join(" และ", used.Select(r => ApproachFull(r.ApproachType)))} ในการสรุปมูลค่า";
            var clauses = new List<string>();
            foreach (var approach in used)
            {
                var parts = Parts(approach);
                if (parts.Count == 0)
                    continue;
                var clause = string.Join(" และ", parts);
                clauses.Add(used.Count > 1 ? $"{ApproachShort(approach.ApproachType)} {clause}" : clause);
            }
            if (clauses.Count > 0)
                sentence += " โดย" + string.Join(" ส่วน", clauses);
            yield return sentence;
        }

        if (reference.Count > 0)
        {
            var names = string.Join(" และ", reference.Select(r => ApproachFull(r.ApproachType)));
            yield return used.Count > 0
                ? $"ทั้งนี้ ได้ใช้{names} เป็นวิธีอ้างอิงประกอบการพิจารณา"   // straight under its own group's paragraph 1: the group is clear from position
                : $"{lead}ใช้{names} เป็นวิธีอ้างอิงประกอบการพิจารณา";
        }
    }

    /// <summary>The section's paragraphs, groups in order; empty when no approach was analysed.</summary>
    public static IReadOnlyList<string> Build(IReadOnlyList<ApproachValueRow> rows, bool multiGroup)
    {
        var paragraphs = new List<string>();
        foreach (var group in rows.GroupBy(r => r.GroupNumber).OrderBy(g => g.Key))
            paragraphs.AddRange(ForGroup(group.ToList(), multiGroup ? $"กลุ่มที่ {group.Key} " : null));
        return paragraphs;
    }
}
