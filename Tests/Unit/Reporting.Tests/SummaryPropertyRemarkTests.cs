using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Providers;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>
/// Each property's own หมายเหตุ (the remark typed on its page) prints at the end of its text in the
/// รายการทรัพย์สิน cell of the land-building, condo and machine summaries. Provider rules are tested on
/// the real helpers; printing is tested by rendering the real templates through the app's renderer.
/// </summary>
public class SummaryPropertyRemarkTests
{
    private static readonly Guid Group = Guid.NewGuid();

    private static async Task<string> RenderAsync(string templateFile, AppraisalSummaryModel model)
    {
        var template = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Templates", templateFile));
        return await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance)
            .RenderRawAsync(template, model, CancellationToken.None);
    }

    private static AppraisalSummaryModel Model(params SummaryGroupRow[] groups) => new() { Groups = [.. groups] };

    private static int Count(string html, string needle) =>
        html.Split(needle).Length - 1;

    private static SummaryRemark R(string text, string? scope = null) => SummaryRemark.From(text, scope)!;

    // The shared markup every remark prints: the label, then the text on the line below.
    private static string Markup(string label, string text) =>
        $"<div class=\"brk\">{label}</div><div class=\"brk\">{text}</div>";

    // ── Land: provider rule ───────────────────────────────────────────────────────

    // One property per sequence unless the test says otherwise.
    private static AppraisalSummaryLandBuildingDataProvider.GroupLandFillRow LandRow(
        int seq, string? remark, Guid? property = null) =>
        new()
        {
            PropertyGroupId = Group, SequenceInGroup = seq, Remark = remark,
            AppraisalPropertyId = property ?? new Guid(seq, 0, 0, new byte[8])
        };

    // Same property id as LandRow(seq, …) gives its default, so a title lines up with the land row of that sequence.
    private static AppraisalSummaryLandBuildingDataProvider.GroupTitleRow Title(
        int seq, string number, string type = "DEED", Guid? property = null) => new()
        {
            PropertyGroupId = Group, SequenceInGroup = seq, TitleId = Guid.NewGuid(), TitleNumber = number, TitleType = type,
            AppraisalPropertyId = property ?? new Guid(seq, 0, 0, new byte[8])
        };

    private static readonly HashSet<(Guid, string)> NothingUnderBuilding = [];

    private static List<string> Lines(
        AppraisalSummaryLandBuildingDataProvider.GroupLandFillRow[] land,
        AppraisalSummaryLandBuildingDataProvider.GroupTitleRow[]? titles = null,
        HashSet<(Guid, string)>? underBuilding = null) =>
        [.. AppraisalSummaryLandBuildingDataProvider
            .LandRemarkLines(land, titles ?? [], underBuilding ?? NothingUnderBuilding)
            .Select(r => $"{r.Label} {r.Text}")];

    [Fact]
    public void Land_remarks_follow_group_order_and_skip_blank_or_dash_only()
    {
        // Four land properties, none with a deed number: the label stays unscoped.
        Lines([LandRow(3, "  third "), LandRow(1, "first"), LandRow(2, "   "), LandRow(4, "-")])
            .Should().Equal("หมายเหตุ: first", "หมายเหตุ:   third");   // label + " " + text, which keeps its 2 leading spaces
    }

    [Fact]
    public void A_land_property_fanned_into_two_rows_by_a_duplicate_parameter_prints_its_remark_once()
    {
        var same = Guid.NewGuid();

        Lines([LandRow(1, "ซ้ำ", same), LandRow(1, "ซ้ำ", same)]).Should().Equal("หมายเหตุ: ซ้ำ");
    }

    [Fact]
    public void A_single_land_property_prints_a_plain_label_even_with_deeds()
    {
        Lines([LandRow(1, "แปลงเดียว")], [Title(1, "12346")]).Should().Equal("หมายเหตุ: แปลงเดียว");
    }

    [Fact]
    public void Two_land_properties_each_name_their_own_deeds()
    {
        Lines(
                [LandRow(2, "แปลงสอง"), LandRow(1, "แปลงแรก")],
                [Title(1, "12346"), Title(1, "12347"), Title(2, "555", "NS3")])
            .Should().Equal(
                "หมายเหตุ (โฉนดที่ดิน 12346, 12347): แปลงแรก",
                "หมายเหตุ (น.ส.3 555): แปลงสอง");
    }

    [Fact]
    public void Two_land_properties_name_the_deed_even_when_only_one_has_a_remark()
    {
        Lines([LandRow(1, "แปลงแรก"), LandRow(2, null)], [Title(1, "12346")])
            .Should().Equal("หมายเหตุ (โฉนดที่ดิน 12346): แปลงแรก");
    }

    [Fact]
    public void Two_land_properties_sharing_a_sequence_each_keep_their_remark()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        // Same sequence, different properties: each remark names only its own deed.
        Lines(
                [LandRow(1, "แปลงแรก", first), LandRow(1, "แปลงสอง", second)],
                [Title(1, "111", property: first), Title(1, "222", property: second)])
            .Should().BeEquivalentTo(
                "หมายเหตุ (โฉนดที่ดิน 111): แปลงแรก",
                "หมายเหตุ (โฉนดที่ดิน 222): แปลงสอง");
    }

    [Fact]
    public void A_land_property_without_a_remark_yields_no_line()
    {
        Lines([LandRow(1, null)]).Should().BeEmpty();
    }

    // ── Land: printing ────────────────────────────────────────────────────────────

    private static SummaryGroupRow LandGroup(List<SummaryRemark> remarks, bool split) => new()
    {
        GroupNumber = 1, PropertyType = "ที่ดิน", SplitLandAndBuilding = split, HasLand = split, LandRowLabel = "ที่ดิน",
        LandDescriptions = ["โฉนดที่ดินเลขที่ 1"],
        LandDeductionLines = ["แนวสายส่งไฟฟ้า ประมาณ 0-0-10 ไร่"], LandNetAreaText = "0-0-90 ไร่ หรือ 90 ตารางวา",
        LandRemarks = remarks
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Two_land_remarks_print_one_line_each_after_the_remaining_area_line(bool split)
    {
        var html = await RenderAsync("appraisal-summary-land-building.html", Model(LandGroup([R("แรก"), R("สอง", "โฉนดที่ดิน 12346")], split)));

        html.Should().Contain(
            "คงเหลือ เนื้อที่ประเมินเพียง 0-0-90 ไร่ หรือ 90 ตารางวา</div></div>" +
            Markup("หมายเหตุ:", "แรก") + Markup("หมายเหตุ (โฉนดที่ดิน 12346):", "สอง"));
        // The figures no longer end level with คงเหลือ, so they must not be bottom-aligned.
        html.Should().NotContain("num bottom");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_land_without_a_remark_prints_nothing_and_keeps_its_alignment(bool split)
    {
        var html = await RenderAsync("appraisal-summary-land-building.html", Model(LandGroup([], split)));

        html.Should().NotContain("<div class=\"brk\">หมายเหตุ");
        html.Should().Contain("num bottom");
    }

    [Fact]
    public async Task Remark_is_html_encoded_and_keeps_its_line_breaks_in_a_wrapping_block()
    {
        var html = await RenderAsync("appraisal-summary-land-building.html",
            Model(LandGroup([R("<b>x</b>\nบรรทัดสอง")], split: false)));

        html.Should().Contain(Markup("หมายเหตุ:", "&lt;b&gt;x&lt;/b&gt;\nบรรทัดสอง"));
        html.Should().NotContain("<b>x</b>");
    }

    // ── Building ──────────────────────────────────────────────────────────────────

    private static readonly Guid Property = Guid.NewGuid();

    private static AppraisalSummaryLandBuildingDataProvider.GroupBuildingRow Bld(Guid id, string? remark) => new()
    {
        BuildingId = id, AppraisalPropertyId = Property, BuildingTypeDisplay = "รั้ว", Remark = remark
    };

    private static AppraisalSummaryLandBuildingDataProvider.GroupDepreciationRow Dep(Guid buildingId, bool isBuilding, Guid? property = null) => new()
    {
        BuildingAppraisalDetailId = buildingId, AppraisalPropertyId = property ?? Property, IsBuilding = isBuilding,
        AreaDescription = "ลาน", Area = 10m, Year = 1, PriceAfterDepreciation = 100m
    };

    private static List<(Guid AppraisalPropertyId, bool IsMoved, SummaryItemRow Row)> DevRows(
        Guid id, string? remark, params AppraisalSummaryLandBuildingDataProvider.GroupDepreciationRow[] deps) =>
        AppraisalSummaryLandBuildingDataProvider.BuildDevelopmentRows(
            [Bld(id, remark)], deps, AppraisalSummaryLandBuildingDataProvider.DevelopmentOnlyBuildingIds(deps), _ => null);

    [Fact]
    public void A_development_only_building_carries_its_remark_on_its_single_line()
    {
        var id = Guid.NewGuid();

        var rows = DevRows(id, " ซ่อมแล้ว ", Dep(id, false), Dep(id, false));

        rows.Should().ContainSingle().Which.Row.Remark.Should().Be(R(" ซ่อมแล้ว "));
    }

    [Fact]
    public void A_normal_buildings_development_rows_never_carry_the_remark()
    {
        var id = Guid.NewGuid();

        var rows = DevRows(id, "ของอาคาร", Dep(id, true), Dep(id, false), Dep(id, false));

        rows.Should().HaveCount(2).And.OnlyContain(r => r.Row.Remark == null);
    }

    private static SummaryGroupRow SplitBuildingGroup(string? remark, bool underConstruction)
    {
        var line = new SummaryItemRow { Description = "บ้านเดี่ยว", Value = 100m, CurrentValue = 50m, Remark = remark is null ? null : R(remark) };
        var dev = new SummaryItemRow { Description = "ลาน", Value = 10m, CurrentValue = 5m };
        return new SummaryGroupRow
        {
            GroupNumber = 1, PropertyType = "สิ่งปลูกสร้าง", SplitLandAndBuilding = true, HasBuilding = true,
            BuildingRowLabel = "สิ่งปลูกสร้าง", BuildingRowLabelUnderConstruction = "สิ่งปลูกสร้าง (แบบแปลน)",
            Buildings = [line], DevelopmentItems = [dev],
            BuildingsCompleted = underConstruction ? [] : [line], DevelopmentItemsCompleted = underConstruction ? [] : [dev],
            BuildingsUnderConstruction = underConstruction ? [line] : [], DevelopmentItemsUnderConstruction = underConstruction ? [dev] : []
        };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_building_remark_prints_once_under_its_building_line(bool underConstruction)
    {
        var model = new AppraisalSummaryModel
        {
            Groups = [SplitBuildingGroup("ต่อเติมครัว", underConstruction)], HasUnderConstruction = underConstruction
        };

        var html = await RenderAsync("appraisal-summary-land-building.html", model);

        Count(html, "ต่อเติมครัว").Should().Be(1);
        html.Should().Contain("บ้านเดี่ยว" + Markup("หมายเหตุ:", "ต่อเติมครัว") + "</td>");
    }

    [Fact]
    public async Task A_blank_building_remark_adds_nothing()
    {
        var html = await RenderAsync("appraisal-summary-land-building.html", Model(SplitBuildingGroup(null, false)));

        html.Should().NotContain("<div class=\"brk\">หมายเหตุ");
    }

    [Fact]
    public async Task A_development_only_line_prints_its_remark_on_the_split_layout()
    {
        var line = new SummaryItemRow { Description = "รั้ว", Value = 10m, CurrentValue = 10m, Remark = R("สร้างใหม่") };
        var group = new SummaryGroupRow
        {
            GroupNumber = 1, PropertyType = "สิ่งปลูกสร้าง", SplitLandAndBuilding = true, HasBuilding = true,
            BuildingRowLabel = "สิ่งปลูกสร้าง", DevelopmentItems = [line], DevelopmentItemsCompleted = [line]
        };

        var html = await RenderAsync("appraisal-summary-land-building.html", Model(group));

        Count(html, "สร้างใหม่").Should().Be(1);
    }

    // ── Display: label, text on the next line, leading spaces kept ───────────

    [Fact]
    public async Task The_label_is_plain_on_its_own_line_and_the_text_starts_on_the_line_below_it()
    {
        var html = await RenderAsync("appraisal-summary-land-building.html", Model(LandGroup([R("1. Test\n2. Test")], split: false)));

        html.Should().Contain(
            "<div class=\"brk\">หมายเหตุ:</div><div class=\"brk\">1. Test\n2. Test</div>");
        html.Should().NotContain("<strong>หมายเหตุ");
    }

    [Fact]
    public async Task A_remark_keeps_the_leading_spaces_the_appraiser_typed_but_not_the_trailing()
    {
        var html = await RenderAsync("appraisal-summary-land-building.html",
            Model(LandGroup([R("    เยื้องเข้ามา \n  บรรทัดสอง   ")], split: false)));

        // Nothing between the tag and the first typed space, so a pre-wrap block shows the indentation.
        html.Should().Contain("<div class=\"brk\">    เยื้องเข้ามา \n  บรรทัดสอง</div>");
    }

    [Theory]
    [InlineData("\r\n\r\n   x", "   x")]
    [InlineData("\n \t \n  x\ny", "  x\ny")]
    [InlineData("x\n\n   y", "x\n\n   y")]   // blank lines inside the text are the appraiser's
    public void Leading_blank_lines_are_dropped_but_the_first_lines_indentation_is_kept(string raw, string expected)
    {
        SummaryRemark.From(raw)!.Text.Should().Be(expected);
    }

    [Fact]
    public void Two_land_properties_sharing_a_sequence_print_in_a_stable_order()
    {
        var a = new Guid(1, 0, 0, new byte[8]);
        var b = new Guid(2, 0, 0, new byte[8]);

        var forward = Lines([LandRow(1, "สอง", b), LandRow(1, "หนึ่ง", a)]);
        var reverse = Lines([LandRow(1, "หนึ่ง", a), LandRow(1, "สอง", b)]);

        forward.Should().Equal(reverse);
    }

    [Fact]
    public void A_blank_or_dash_only_remark_is_no_remark()
    {
        SummaryRemark.From("   ").Should().BeNull();
        SummaryRemark.From("-").Should().BeNull();
        SummaryRemark.From("  x  ")!.Text.Should().Be("  x");
    }

    // ── Condo ─────────────────────────────────────────────────────────────────────

    private static SummaryGroupRow CondoGroup(params (string Text, string? Remark)[] units) => new()
    {
        GroupNumber = 1, PropertyType = "ห้องชุด", CollateralDetails = string.Join(" | ", units.Select(u => u.Text)),
        DetailItems = [.. units.Select(u => new SummaryItemRow { Description = u.Text, Remark = u.Remark is null ? null : R(u.Remark) })]
    };

    [Fact]
    public async Task Each_condo_unit_prints_its_own_remark_under_its_line()
    {
        var html = await RenderAsync("appraisal-summary-condo.html",
            Model(CondoGroup(("ห้อง 1", null), ("ห้อง 2", "ชำรุด"))));

        html.Should().Contain("<div>- ห้อง 1</div><div>- ห้อง 2</div>" + Markup("หมายเหตุ:", "ชำรุด") + "</td>");
        Count(html, "ชำรุด").Should().Be(1);
    }

    [Fact]
    public async Task A_single_condo_unit_prints_its_remark_in_the_plain_cell()
    {
        var html = await RenderAsync("appraisal-summary-condo.html", Model(CondoGroup(("ห้อง 1", "ชำรุด"))));

        html.Should().Contain("<td>ห้อง 1" + Markup("หมายเหตุ:", "ชำรุด") + "</td>");
    }

    [Fact]
    public async Task A_condo_unit_without_a_remark_prints_nothing()
    {
        var html = await RenderAsync("appraisal-summary-condo.html", Model(CondoGroup(("ห้อง 1", null))));

        html.Should().NotContain("<div class=\"brk\">หมายเหตุ").And.Contain("<td>ห้อง 1</td>");
    }

    [Fact]
    public async Task A_condo_group_with_no_units_still_renders()
    {
        var html = await RenderAsync("appraisal-summary-condo.html", Model(CondoGroup()));

        html.Should().NotContain("<div class=\"brk\">หมายเหตุ");
    }

    [Fact]
    public void The_market_row_lists_only_development_only_properties_that_have_a_line()
    {
        var withLine = new SummaryItemRow { Description = "รั้ว", Remark = R("สร้างใหม่") };
        var noLine = new SummaryItemRow { Description = null };
        var noRemark = new SummaryItemRow { Description = "ลาน" };
        var normalRow = new SummaryItemRow { Description = "ถังน้ำ", Remark = R("ไม่ควรขึ้น") };

        var lines = AppraisalSummaryLandBuildingDataProvider.DevelopmentDescriptions(
            [(true, withLine), (true, noLine), (true, noRemark), (false, normalRow)]);

        lines.Should().Equal(withLine, noRemark);
    }

    // ── LB / LS: an equal remark prints once, under the building ──────────────────

    private static readonly Guid Lb = Guid.NewGuid();

    private static SummaryItemRow BuildingLine(string? description, string? remark) =>
        new() { Description = description, Value = 100m, CurrentValue = 100m, Remark = remark is null ? null : R(remark) };

    private static List<SummaryRemark> LandRemarksGiven(
        string landRemark, HashSet<(Guid Property, string Remark)> underBuilding) =>
        AppraisalSummaryLandBuildingDataProvider.LandRemarkLines([LandRow(1, landRemark, Lb)], [], underBuilding);

    private static List<string> LandLinesGiven(
        string landRemark, HashSet<(Guid Property, string Remark)> underBuilding) =>
        [.. LandRemarksGiven(landRemark, underBuilding).Select(r => $"{r.Label} {r.Text}")];

    [Fact]
    public void An_LB_market_building_block_carries_an_equal_remark_and_the_land_copy_is_dropped()
    {
        var underBuilding = AppraisalSummaryLandBuildingDataProvider.RemarksUnderBuildingLines(
            [(Lb, "เหมือนกัน")], []);

        LandLinesGiven("เหมือนกัน", underBuilding).Should().BeEmpty();
    }

    [Fact]
    public void A_development_only_LB_line_carries_an_equal_remark_and_the_land_copy_is_dropped()
    {
        var underBuilding = AppraisalSummaryLandBuildingDataProvider.RemarksUnderBuildingLines(
            [], [(Lb, true, BuildingLine("รั้ว", "เหมือนกัน"))]);

        LandLinesGiven("เหมือนกัน", underBuilding).Should().BeEmpty();
    }

    [Fact]
    public void An_LB_with_differing_remarks_prints_both()
    {
        var underBuilding = AppraisalSummaryLandBuildingDataProvider.RemarksUnderBuildingLines(
            [(Lb, "ของอาคาร")], []);

        LandLinesGiven("ของที่ดิน", underBuilding).Should().Equal("หมายเหตุ: ของที่ดิน");
        underBuilding.Should().Contain((Lb, "ของอาคาร"));
    }

    [Fact]
    public void An_LB_development_only_property_with_no_line_carries_no_remark_so_the_land_copy_stays()
    {
        var id = Guid.NewGuid();
        // Nothing recorded on the building: no description, so its remark is not set on the row at all.
        var deps = new[] { Dep(id, false, Lb) };
        var rows = AppraisalSummaryLandBuildingDataProvider.BuildDevelopmentRows(
            [new AppraisalSummaryLandBuildingDataProvider.GroupBuildingRow
                { BuildingId = id, AppraisalPropertyId = Lb, Remark = "เหมือนกัน" }],
            deps, AppraisalSummaryLandBuildingDataProvider.DevelopmentOnlyBuildingIds(deps), _ => null);

        rows.Should().ContainSingle().Which.Row.Remark.Should().BeNull();
        var underBuilding = AppraisalSummaryLandBuildingDataProvider.RemarksUnderBuildingLines(
            [], rows.Select(r => (r.AppraisalPropertyId, r.IsMoved, r.Row)));
        LandLinesGiven("เหมือนกัน", underBuilding).Should().Equal("หมายเหตุ: เหมือนกัน");
    }

    [Fact]
    public void A_normal_buildings_development_row_does_not_stand_in_for_its_building()
    {
        var underBuilding = AppraisalSummaryLandBuildingDataProvider.RemarksUnderBuildingLines(
            [], [(Lb, false, BuildingLine("ลาน", "เหมือนกัน"))]);

        LandLinesGiven("เหมือนกัน", underBuilding).Should().Equal("หมายเหตุ: เหมือนกัน");
    }

    [Fact]
    public async Task An_LB_group_with_an_equal_remark_prints_it_exactly_once_under_the_building()
    {
        // An LB/LS group is never split (HasCombinedProperty), so this is the market row.
        var line = BuildingLine("บ้าน", "ซ้ำ");
        var underBuilding = AppraisalSummaryLandBuildingDataProvider.RemarksUnderBuildingLines([(Lb, "ซ้ำ")], []);
        var group = new SummaryGroupRow
        {
            GroupNumber = 1, PropertyType = "ที่ดินพร้อมสิ่งปลูกสร้าง", SplitLandAndBuilding = false, HasLand = true,
            HasBuilding = true, LandRowLabel = "ที่ดิน", BuildingRowLabel = "สิ่งปลูกสร้าง",
            LandDescriptions = ["โฉนด 1"], LandRemarks = LandRemarksGiven("ซ้ำ", underBuilding),
            BuildingClauses = [line]
        };

        var html = await RenderAsync("appraisal-summary-land-building.html", Model(group));

        Count(html, "ซ้ำ").Should().Be(1);
    }

    // ── Machine ───────────────────────────────────────────────────────────────────

    private static AppraisalSummaryMachineDataProvider.GroupMachineDetailRow Machine(string name, string? remark) => new()
    {
        PropertyGroupId = Group, MachineName = name, RegistrationStatus = true, IsPriceCertified = true, Remark = remark
    };

    [Fact]
    public void Machine_remarks_stay_aligned_with_their_machines()
    {
        var section = AppraisalSummaryMachineDataProvider.BuildMachineSections(
            [Machine("ปั๊ม", null), Machine("เครน", "  เสียบางส่วน ")]).Should().ContainSingle().Subject;

        section.Items.Select(i => (i.Description, i.Remark))
            .Should().Equal(("ปั๊ม", (SummaryRemark?)null), ("เครน", R("  เสียบางส่วน ")));
    }

    [Fact]
    public async Task Each_machine_prints_its_own_remark_under_its_line()
    {
        var sections = AppraisalSummaryMachineDataProvider.BuildMachineSections(
            [Machine("ปั๊ม", null), Machine("เครน", "เสียบางส่วน")]);
        var group = new SummaryGroupRow { GroupNumber = 1, PropertyType = "เครื่องจักร", MachineSections = sections };

        var html = await RenderAsync("appraisal-summary-machine.html", Model(group));

        html.Should().Contain("<div class=\"m-item\">2. เครน</div>" + Markup("หมายเหตุ:", "เสียบางส่วน"));
        Count(html, "เสียบางส่วน").Should().Be(1);
    }
}
