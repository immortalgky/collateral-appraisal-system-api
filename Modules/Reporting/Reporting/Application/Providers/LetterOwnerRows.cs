using Reporting.Application.Formatting;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers;

/// <summary>
/// The external letter's owner rows (land, building, condo) and its อาคารสิ่งปลูกสร้าง row, built from every item of the appraisal
/// rather than the first one. Land and building rows come from the sections the book has already loaded, so "หลังที่ N" is the same
/// number the book's building table and cost table print; the condo rows come from the builder's own query.
/// </summary>
internal static class LetterOwnerRows
{
    /// <summary>Up to this many items an owner row pairs each owner with its item; above it, it counts per owner.</summary>
    private const int MaxPaired = 10;

    /// <summary>
    /// One value for all items: that value. Several values and up to 10 items: one "{item}: {value}" line per item. More than 10:
    /// one "{value} จำนวน N {unit}" line per value, N adding up each item's count, with " (รายละเอียดตามข้อ {section})" on the last line.
    /// An item with no value is left out; none at all gives null. Several lines are separated by "\n"; the letter prints one line per
    /// entry. Used for the owner rows and for the obligation rows. <paramref name="group"/> = false never groups (the book's per-room lines, however many there are). A null <paramref name="section"/> prints no "(รายละเอียดตามข้อ …)" pointer (the condo texts
    /// of the mixed book and of the condo summary have no section to point to; the letter's condo rows point to 2.1 only in a condo book, see <see cref="CondoPointer"/>).
    /// <paramref name="noneText"/> (the obligation and eviction rows pass "ไม่มี") is the value that means "nothing recorded": such items are never
    /// listed or counted, only the items that do have an entry are, so a few deeds with an entry do not read "ไม่มี จำนวน 45 ฉบับ". When every item
    /// is none the row reads that text; when only some are, the entries are paired or counted even if they are all the same (a lone value would
    /// otherwise read as if it applied to every deed). An owner is never "none", so the owner rows pass nothing.
    /// </summary>
    public static string? Summarise(IReadOnlyList<(string Label, string? Text, int Count)> items, string unit, string? section, string? noneText = null, bool group = true)
    {
        var all = items
            .Select(i => (i.Label, Text: ThaiAddressFormatter.Stated(i.Text), i.Count))
            .Where(i => i.Text is not null)
            .Select(i => (i.Label, Text: i.Text!, i.Count))
            .ToList();
        if (all.Count == 0) return null;

        var owned = noneText is null ? all : all.Where(i => i.Text != noneText).ToList();
        if (owned.Count == 0) return noneText;

        var values = owned.Select(i => i.Text).Distinct().ToList();
        if (values.Count == 1 && owned.Count == all.Count) return values[0];
        if (owned.Count <= MaxPaired || !group)
            return string.Join("\n", owned.Select(i => $"{i.Label}: {i.Text}"));

        return string.Join("\n", values.Select(v => $"{v} จำนวน {owned.Where(i => i.Text == v).Sum(i => i.Count)} {unit}"))
               + (string.IsNullOrWhiteSpace(section) ? "" : $" (รายละเอียดตามข้อ {section})");
    }

    /// <summary>
    /// One item per numbered deed: "โฉนดที่ดิน 12345", with the text its land gives for it. The deeds are listed in 2.1 (the construction book's section 2).
    /// A land with no numbered deed still counts, as "ที่ดินแปลงที่ N", so its row is never dropped or replaced by another land's.
    /// <paramref name="perTitle"/> (the owners, which differ per deed row) also gives every deed row without a number its own item: "ที่ดินแปลงที่ N" when the land has no
    /// numbered row, "ที่ดินแปลงที่ N (ไม่ระบุเลขที่)" next to numbered ones. A land-level text (eviction, obligation) is the same on every row, so it stays one item.
    /// </summary>
    private static List<(string Label, string? Text, int Count)> LandItems(AppraisalSummaryModel model, Func<LandSection, LandTitleRow?, string?> text, bool perTitle = false)
    {
        var items = new List<(string Label, string? Text, int Count)>();
        var lands = model.AllLands;
        for (var i = 0; i < lands.Count; i++)
        {
            var land = lands[i];
            var numbered = land.Titles.Where(t => ThaiAddressFormatter.Stated(t.TitleNumber) is not null).ToList();
            var unnumbered = land.Titles.Where(t => ThaiAddressFormatter.Stated(t.TitleNumber) is null).ToList();
            items.AddRange(numbered.Select(t => ($"{t.TitleTypeDisplay} {t.TitleNumber!.Trim()}", text(land, t), 1)));
            if (numbered.Count == 0 && (!perTitle || unnumbered.Count == 0))
                items.Add(($"ที่ดินแปลงที่ {i + 1}", text(land, unnumbered.FirstOrDefault()), 1));
            else if (perTitle)
                items.AddRange(unnumbered.Select(t => ($"ที่ดินแปลงที่ {i + 1}{(numbered.Count > 0 ? " (ไม่ระบุเลขที่)" : "")}", text(land, t), 1)));
        }
        return items;
    }

    private static string? LandRows(AppraisalSummaryModel model, Func<LandSection, LandTitleRow?, string?> text, string standardSection = "2.1", string? noneText = null, bool perTitle = false) =>
        Summarise(LandItems(model, text, perTitle), "ฉบับ", model.BodyType == "construction" ? "2" : standardSection, noneText);

    /// <summary>
    /// การรอนสิทธิ of every deed on the external summary page: the eviction type(s) as the book prints them in 2.2, without the typed remark (the remark stays in 2.2);
    /// a land with none recorded reads "ไม่มี", like the obligation rows beside it. Null when there is no land.
    /// </summary>
    public static string? LandEvictions(AppraisalSummaryModel model) =>
        LandRows(model, (l, _) => ThaiAddressFormatter.Stated(l.EvictionType) ?? ObligationText.None, "2.2", ObligationText.None);

    /// <summary>The deeds' owners: "โฉนดที่ดิน 12345: {owner}".</summary>
    public static string? LandOwners(AppraisalSummaryModel model) => LandRows(model, (_, t) => t?.OwnerName, perTitle: true);

    /// <summary>
    /// The ภาระผูกพัน and ภาระผูกพันอื่นๆ rows (each collapses on its own): one item per numbered deed of every land, one per condo room, the "ไม่มี" rule
    /// of <see cref="Summarise"/>. A land with no numbered deed still counts, as "ที่ดินแปลงที่ N", so its mortgage is never dropped (or replaced by another
    /// land's). Null when there is no land and no condo room.
    /// </summary>
    public static (string? Mortgage, string? Others) Obligations(AppraisalSummaryModel model)
    {
        var condo = model.CondoObligationItems;
        if (model.AllLands.Count == 0 && condo.Count == 0) return (null, null);

        List<(string Label, string? Text, int Count)> Items(Func<LandSection, string?> landText, Func<ExternalObligationItem, string?> condoText)
        {
            var items = LandItems(model, (l, _) => landText(l));
            items.AddRange(condo.Select(c => (c.Label, condoText(c), 1)));
            return items;
        }

        var unit = model.AllLands.Count > 0 && condo.Count > 0 ? "รายการ" : condo.Count > 0 ? "ห้อง" : "ฉบับ";
        // the pointer has to find every grouped item there: the land deeds are in 2.1 (2 in the construction book), the rooms only in a condo book's 2.1
        var section = condo.Count == 0 ? (model.BodyType == "construction" ? "2" : "2.1") : model.AllLands.Count == 0 ? CondoPointer(model) : null;
        return (Summarise(Items(l => l.ObligationMortgage, c => c.Mortgage), unit, section, ObligationText.None),
                Summarise(Items(l => l.ObligationOthers, c => c.Others), unit, section, ObligationText.None));
    }

    /// <summary>The letter's condo rows point to 2.1 only in a condo book, whose 2.1 lists every room; in a land + condo book 2.1 is the land deeds, so there is no pointer.</summary>
    private static string? CondoPointer(AppraisalSummaryModel model) => model.BodyType == "condo" ? "2.1" : null;

    /// <summary>The condo rooms' owners on the letter: "ห้อง 101: {owner}" per room, grouped above 10 (see <see cref="Summarise"/>).</summary>
    public static string? CondoOwners(AppraisalSummaryModel model) =>
        Summarise(model.CondoObligationItems.Select(c => (c.Label, c.Owner, 1)).ToList(), "ห้อง", CondoPointer(model));

    /// <summary>
    /// The appraised buildings, not the development-only items: the set the cover counts and the book numbers "รายการที่ N".
    /// </summary>
    internal static List<BuildingSection> Counted(AppraisalSummaryModel model) =>
        model.AllBuildings.Where(b => !b.IsDevelopmentOnly && b.IsAppraisable != false).ToList();

    /// <summary>The buildings' owners: "หลังที่ 2: {owner}". An unverified owner prints as the letter always has ("ไม่สามารถตรวจสอบได้").</summary>
    public static string? BuildingOwners(AppraisalSummaryModel model) =>
        Summarise(
            Counted(model).Select(b => ($"หลังที่ {b.BuildingNumber}", b.OwnerText, 1)).ToList(),
            "หลัง", BuildingSections(model));

    /// <summary>
    /// The section(s) that print the building table: 3.n for every group that has buildings, numbered the way book-flow-standard numbers
    /// them (one 3.n per group with lands, then one per group with buildings). The construction-inspection book lists them in section 3.
    /// </summary>
    internal static string BuildingSections(AppraisalSummaryModel model)
    {
        if (model.BodyType != "standard") return "3";
        var n = 0;
        var sections = new List<string>();
        foreach (var g in model.PropertyGroups)
        {
            if (g.Lands.Count > 0) n++;
            if (g.RealBuildings.Count > 0) sections.Add($"3.{++n}");
        }
        return sections.Count > 0 ? string.Join(", ", sections) : "3";
    }

    /// <summary>
    /// "บ้านเดี่ยว 3 ชั้น แบบ A1 จำนวน 2 หลัง" and "โรงจอดรถ 1 ชั้น จำนวน 1 หลัง" on separate lines: the buildings grouped by (type, floors, model) in the order
    /// they first appear. One building prints as it always did, with no count. A blank or dash-only type or model is left out.
    /// </summary>
    public static string? BuildingDetails(AppraisalSummaryModel model)
    {
        var buildings = Counted(model);
        var groups = buildings
            .GroupBy(b => (Type: ThaiAddressFormatter.Stated(b.BuildingTypeText), b.NumberOfFloors, Model: ThaiAddressFormatter.Stated(b.ModelName)))
            .Select(g => (Text: Describe(g.Key.Type, g.Key.NumberOfFloors, g.Key.Model), Count: g.Count()))
            .ToList();

        if (buildings.Count == 0) return null;
        if (buildings.Count == 1) return groups[0].Text.Length > 0 ? groups[0].Text : null;
        return string.Join("\n", groups.Select(g => $"{(g.Text.Length > 0 ? g.Text : "สิ่งปลูกสร้าง")} จำนวน {g.Count} หลัง"));
    }

    private static string Describe(string? type, decimal? floors, string? modelName)
    {
        var parts = new List<string>();
        if (type is not null) parts.Add(type);
        if (floors.HasValue) parts.Add($"{floors:#,##0.##} ชั้น");
        if (modelName is not null) parts.Add($"แบบ {modelName}");
        return string.Join(" ", parts);
    }
}
