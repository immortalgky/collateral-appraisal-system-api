using Reporting.Application.Formatting;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers;

/// <summary>
/// The cover page's property block: up to four centred lines (what it is, how big, where; an inspection line for a construction
/// inspection), built from the sections the book already loaded so the internal and the external cover print the same wording.
/// Covered: land / land + buildings / buildings, condo and machinery appraisals and any mix of those families, block (project)
/// appraisals and construction inspections. Anything else returns no lines and the cover keeps its old
/// "ประเภททรัพย์สิน" / "ที่ตั้งทรัพย์สิน" lines.
/// </summary>
internal static class CoverNarrative
{
    /// <summary>The company letter's ประเภททรัพย์สิน row: the first line of the cover block (<see cref="AppraisalSummaryModel.CoverLines"/>, built once), or the summary the builder wrote when the cover has no block.</summary>
    public static string? LetterPropertyType(AppraisalSummaryModel model) => model.CoverLines.FirstOrDefault() ?? model.PropertyTypeSummary;

    public static IReadOnlyList<string> Build(AppraisalSummaryModel model) => model.BodyType switch
    {
        "standard" or "condo" or "machine" => Standard(model),
        "block" => Block(model),
        "construction" => Construction(model),
        _ => []
    };

    // ── land / building / condo / machine, alone or mixed ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether "และส่วนพัฒนาอื่นๆ" applies: the same rule as the 3.2 text, which prints it whenever the group has a ส่วนพัฒนา item (the non-building rows of
    /// a real building, a fence, as well as a development-only property) whose owning property is not marked IsAppraisable = false.
    /// </summary>
    internal static bool HasAppraisedDevelopment(AppraisalSummaryModel model) => model.AllDevelopmentItems.Any(i => i.IsAppraisable != false);

    private static IReadOnlyList<string> Standard(AppraisalSummaryModel model)
    {
        var lands = model.AllLands;
        var buildings = model.AllBuildings;
        var condo = model.CondoSection;
        var hasCondo = condo is not null && condo.Units.Count > 0;
        var machines = model.MachineSection?.Machines.Count ?? 0;

        // The same count rule as the section 6 cost summary: an explicit IsAppraisable = false is out, development-only items are not buildings.
        var nBuildings = buildings.Count(b => !b.IsDevelopmentOnly && b.IsAppraisable != false);
        var hasDev = HasAppraisedDevelopment(model);

        var what = new List<string>();
        var size = new List<string>();

        if (lands.Count > 0 || nBuildings > 0 || hasDev)
        {
            if (lands.Count > 0)
            {
                // No vacancy flag exists on the land, so a land with no appraised building is just "ที่ดิน".
                var dev = hasDev ? "ส่วนพัฒนาอื่นๆ" : null;
                what.Add((nBuildings, dev) switch
                {
                    (> 0, null) => $"ที่ดินพร้อมสิ่งปลูกสร้าง จำนวน {nBuildings} รายการ บนที่ดิน",
                    (> 0, _) => $"ที่ดินพร้อมสิ่งปลูกสร้าง จำนวน {nBuildings} รายการ และ{dev} บนที่ดิน",
                    (_, not null) => $"ที่ดินพร้อม{dev} บนที่ดิน",
                    _ => "ที่ดิน"
                });
                var wa = lands.Sum(l => l.TotalAreaInWa);
                if (wa > 0m) size.Add("เนื้อที่ดินรวม " + ThaiLandAreaFormatter.Describe(wa));
            }
            else if (nBuildings > 0)
                what.Add($"สิ่งปลูกสร้าง จำนวน {nBuildings} รายการ{(hasDev ? " และส่วนพัฒนาอื่นๆ" : "")}");
            else
                what.Add("ส่วนพัฒนาอื่นๆ");   // no land, no counted building: only development items (ExternalSummaryRows prints this block too)
        }

        if (hasCondo)
        {
            what.Add($"ห้องชุด จำนวน {condo!.Units.Count} ห้อง");
            if (condo.UsableArea is > 0m) size.Add($"เนื้อที่ห้องชุดรวม {condo.UsableArea:#,##0.##} ตารางเมตร");
        }

        // One entry per machine row (not the sum of the quantities).
        if (machines > 0)
            what.Add($"เครื่องจักรและอุปกรณ์ จำนวน {machines} รายการ");

        if (what.Count == 0)
            return [];

        return Lines(string.Join(" และ", what), size.Count > 0 ? string.Join(" และ", size) : null, null, Address(model));
    }

    // ── block (project) ───────────────────────────────────────────────────────────────────────────────────────────────

    private static IReadOnlyList<string> Block(AppraisalSummaryModel model)
    {
        if (model.BlockSection is not { } block)
            return [];

        // The appraised units the book's unit tables list; a project with none keyed yet falls back to what its models say.
        var units = block.Project.AppraisedUnitCount > 0 ? block.Project.AppraisedUnitCount : block.Models.Sum(m => m.UnitCount);
        if (units == 0)
            return [];

        var project = ThaiAddressFormatter.Stated(block.Project.ProjectName) is { } name ? $" ในโครงการ {name}" : "";
        var what = block.IsCondo ? $"ห้องชุด จำนวน {units} ห้อง{project}" : $"ที่ดินพร้อมสิ่งปลูกสร้าง จำนวน {units} หน่วย{project}";
        string? size = null;
        if (!block.IsCondo && block.Project.LandAreaSquareWa is { } wa && wa > 0m)
            size = "เนื้อที่ดินโครงการรวม " + ThaiLandAreaFormatter.Describe(wa);

        // The block summary's ที่ตั้งโครงการ (the address alone, without the project name).
        return Lines(what, size, null, ThaiAddressFormatter.Stated(model.ProjectAddress));
    }

    // ── construction inspection ───────────────────────────────────────────────────────────────────────────────────────

    private static IReadOnlyList<string> Construction(AppraisalSummaryModel model)
    {
        var lands = model.AllLands;
        var counted = model.AllBuildings.Where(b => !b.IsDevelopmentOnly && b.IsAppraisable != false).ToList();
        var inspected = model.ConstructionSection?.Buildings.Count ?? 0;
        var n = inspected > 0 ? inspected : counted.Count;
        if (n == 0)
            return [];

        var what = lands.Count > 0 ? $"ที่ดินพร้อมสิ่งปลูกสร้างระหว่างก่อสร้าง จำนวน {n} รายการ บนที่ดิน" : $"สิ่งปลูกสร้างระหว่างก่อสร้าง จำนวน {n} รายการ";
        var wa = lands.Sum(l => l.TotalAreaInWa);
        var size = wa > 0m ? "เนื้อที่ดินรวม " + ThaiLandAreaFormatter.Describe(wa) : null;

        // The cumulative progress is shown for a single building only (several would need one figure each), as the 3.2 item text prints it.
        var round = ThaiAddressFormatter.Stated(model.ConstructionSection?.CurrentInspectionRound);
        decimal? pct = null;
        if (n == 1)
            pct = (counted.Count == 1 ? counted[0].ConstructionProgressPct : null) ?? model.ConstructionSection?.Buildings.FirstOrDefault()?.TotalCurrentPct;
        var inspection = "ตรวจผลงานก่อสร้าง"
            + (round is not null ? $" งวดที่ {round}" : "")
            + (pct is { } p ? $" ผลงานสะสม {BuildingSection.ProgressPctText(p)}%" : "");

        return Lines(what, size, inspection, Address(model));
    }

    // ── shared ────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static string? Address(AppraisalSummaryModel model) =>
        ThaiAddressFormatter.Stated(model.IsExternal ? model.CollateralLocation : model.CollateralAddress);

    /// <summary>"ตั้งอยู่…" for the cover's address and the external summary page: no space before a house number ("เลขที่…"); null without an address.</summary>
    public static string? LocationLine(AppraisalSummaryModel model) => Address(model) is { } address ? Located(address) : null;

    private static string Located(string address) => "ตั้งอยู่" + (address.StartsWith("เลขที่") ? "" : " ") + address;

    /// <summary>The block's lines in order; a missing one is skipped. The address reads "ตั้งอยู่…" (no space before a house number, "เลขที่…").</summary>
    private static IReadOnlyList<string> Lines(string what, string? size, string? inspection, string? address)
    {
        var lines = new List<string> { what };
        if (size is not null) lines.Add(size);
        if (inspection is not null) lines.Add(inspection);
        if (address is not null) lines.Add(Located(address));
        return lines;
    }
}
