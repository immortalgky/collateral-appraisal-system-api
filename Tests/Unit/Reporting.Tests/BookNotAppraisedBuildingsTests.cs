using System.Data;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers;
using Reporting.Application.Providers.Sections;
using Reporting.Tests.Infrastructure;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>Book 3.2: a building the appraiser chose not to value (IsAppraisable == false) is not a table row, is out of the total, and is noted under the table.</summary>
public class BookNotAppraisedBuildingsTests
{
    private static BuildingSection B(string house, decimal area, bool? appraisable = null, string? reason = null, string type = "บ้านเดี่ยว", short? floors = 2) => new()
    {
        HouseNumber = house, BuildingTypeText = type, NumberOfFloors = floors, UsableArea = area, IsAppraisable = appraisable, NotAppraisableReason = reason
    };

    private static async Task<string> Section32Async(params BuildingSection[] buildings)
    {
        var html = await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(
            "{{ include 'partials/book-flow-standard' }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Buildings = buildings }] }, CancellationToken.None);
        var start = html.IndexOf("อาคารสิ่งปลูกสร้าง</span></div>", StringComparison.Ordinal);
        var end = html.IndexOf("<div class=\"bk-bld\">", start, StringComparison.Ordinal);
        return html[start..(end < 0 ? html.IndexOf("<!-- PART", start, StringComparison.Ordinal) : end)];
    }

    private const string TotalLabel = "รวมสิ่งปลูกสร้าง ทรัพย์สินที่ประเมินมูลค่า";

    [Fact]
    public async Task One_excluded_building_among_two_is_no_row_not_in_the_total_not_in_the_count_and_is_noted_with_its_reason()
    {
        var s = await Section32Async(B("10/1", 100m), B("10/2", 40m, appraisable: false, reason: "เป็นโครงสร้างชั่วคราว"));

        s.Should().Contain("จำนวน 1 รายการ").And.Contain("<td>10/1</td>").And.NotContain("<td>10/2</td>")
            .And.Contain($"{TotalLabel}</td><td class=\"r\">100.00</td>").And.NotContain("140.00");
        s.Should().Contain("<p class=\"bk-p\">หมายเหตุ : ผู้ประเมินพิจารณาไม่ประเมินมูลค่าบ้านเดี่ยว 2 ชั้น บ้านเลขที่ 10/2 พื้นที่ใช้สอย 40.00 ตร.ม. เนื่องจากเป็นโครงสร้างชั่วคราว</p>");
    }

    [Fact]
    public async Task Two_excluded_buildings_print_the_lead_once_and_one_indented_line_each()
    {
        var s = await Section32Async(B("10/1", 100m), B("10/2", 40m, false, "ชั่วคราว"), B("10/3", 25.5m, false, "ชำรุดทรุดโทรม", type: "โรงจอดรถ", floors: 1));

        s.Split("ผู้ประเมินพิจารณาไม่ประเมินมูลค่าสิ่งปลูกสร้าง ดังนี้").Length.Should().Be(2);
        s.Should().Contain("<div class=\"bk-p\" style=\"padding-left:32px;\">1. บ้านเดี่ยว 2 ชั้น บ้านเลขที่ 10/2 พื้นที่ใช้สอย 40.00 ตร.ม. เนื่องจากชั่วคราว</div>")
            .And.Contain("<div class=\"bk-p\" style=\"padding-left:32px;\">2. โรงจอดรถ 1 ชั้น บ้านเลขที่ 10/3 พื้นที่ใช้สอย 25.50 ตร.ม. เนื่องจากชำรุดทรุดโทรม</div>")
            .And.Contain("จำนวน 1 รายการ");
        s.Split("ผู้ประเมินพิจารณาไม่ประเมินมูลค่า").Length.Should().Be(2); // only the lead line carries the words
    }

    [Fact]
    public async Task A_blank_reason_drops_the_because_part()
    {
        var s = await Section32Async(B("10/1", 100m), B("10/2", 40m, false, "   "));

        s.Should().Contain("พื้นที่ใช้สอย 40.00 ตร.ม.</p>").And.NotContain("เนื่องจาก");
    }

    [Fact]
    public async Task A_null_reason_drops_the_because_part()
    {
        var s = await Section32Async(B("10/1", 100m), B("10/2", 40m, false, null));

        s.Should().Contain("พื้นที่ใช้สอย 40.00 ตร.ม.</p>").And.NotContain("เนื่องจาก");
    }

    [Fact]
    public async Task When_every_building_is_excluded_there_is_no_table_and_no_intro_just_the_heading_and_the_note()
    {
        var s = await Section32Async(B("10/1", 100m, false, "รื้อถอนแล้ว"));

        s.Should().NotContain("<table").And.NotContain("จำนวน").And.NotContain(TotalLabel)
            .And.Contain("หมายเหตุ : ผู้ประเมินพิจารณาไม่ประเมินมูลค่า").And.Contain("เนื่องจากรื้อถอนแล้ว");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    public async Task Null_or_true_is_appraised_as_before_and_no_note_prints(bool? appraisable)
    {
        var s = await Section32Async(B("10/1", 100m, appraisable));

        s.Should().Contain("จำนวน 1 รายการ").And.Contain("<td>10/1</td>").And.NotContain("ผู้ประเมินพิจารณาไม่ประเมินมูลค่า");
    }

    [Fact]
    public async Task The_total_row_has_the_new_label_and_the_user_text_is_escaped()
    {
        var s = await Section32Async(B("10/1", 100m), B("<b>1</b>", 40m, false, "<script>x</script>"));

        s.Should().Contain(TotalLabel).And.NotContain("รวมพื้นที่อาคาร")
            .And.Contain("&lt;script&gt;x&lt;/script&gt;").And.NotContain("<script>").And.Contain("&lt;b&gt;1&lt;/b&gt;");
    }

    private static async Task<string> FullAsync(params PropertyGroupDetail[] groups) =>
        await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(
            "{{ include 'partials/book-flow-standard' }}", new AppraisalSummaryModel { PropertyGroups = groups }, CancellationToken.None);

    private static List<string> DetailHeadings(string html) =>
        Regex.Matches(html, "<div class=\"vk-head\"><span>(.*?)</span>").Select(m => m.Groups[1].Value).ToList();

    private static PropertyGroupDetail G(int n, params BuildingSection[] b) => new() { GroupNumber = n, Buildings = b };

    [Fact]
    public async Task An_excluded_building_in_the_middle_has_its_detail_last_unnumbered_and_marked_and_the_table_numbers_match()
    {
        var html = await FullAsync(G(1, B("10/1", 100m), B("10/2", 40m, false, "ชั่วคราว"), B("10/3", 60m)));

        DetailHeadings(html).Should().Equal("สิ่งปลูกสร้างรายการที่ 1", "สิ่งปลูกสร้างรายการที่ 2", "สิ่งปลูกสร้าง (ไม่ประเมินมูลค่า)");
        Regex.Matches(html, "<td class=\"c\">(\\d+)</td>\\s*<td>(10/\\d)</td>").Select(m => m.Groups[1].Value + "=" + m.Groups[2].Value)
            .Should().Equal("1=10/1", "2=10/3");
    }

    [Fact]
    public async Task Without_an_excluded_building_the_details_are_numbered_as_before()
    {
        var html = await FullAsync(G(1, B("10/1", 100m), B("10/2", 40m)));

        DetailHeadings(html).Should().Equal("สิ่งปลูกสร้างรายการที่ 1", "สิ่งปลูกสร้างรายการที่ 2");
    }

    [Fact]
    public async Task Numbering_continues_across_groups_over_the_appraised_buildings_only()
    {
        var html = await FullAsync(G(1, B("10/1", 100m), B("10/2", 40m, false)), G(2, B("20/1", 50m)));

        DetailHeadings(html).Should().Equal("สิ่งปลูกสร้างรายการที่ 1", "สิ่งปลูกสร้าง (ไม่ประเมินมูลค่า)", "สิ่งปลูกสร้างรายการที่ 2");
    }

    [Fact]
    public void The_cost_page_number_skips_the_excluded_building()
    {
        var bs = new[] { B("1", 1m), B("2", 1m, false), B("3", 1m) };
        var groups = AppraisalBookDataProvider.BuildPropertyGroups([], bs);

        groups.Single().Buildings.Select(b => b.BuildingNumber).Should().Equal(1, 0, 2);
    }

    [Fact]
    public async Task The_cost_table_lists_the_excluded_building_last_with_the_mark_and_no_number()
    {
        BuildingSection C(string name, int no, bool? ap = null) => new()
        {
            BuildingName = name, BuildingNumber = no, IsAppraisable = ap, CostRows = [new BuildingCostRow { AreaItemName = "x", Area = 1m }]
        };
        var html = await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(
            "{{ cost_x = []; cost_blds = []; for b in model.all_buildings }}{{ if b.is_appraisable == false }}{{ cost_x = cost_x | array.add b }}{{ else }}{{ cost_blds = cost_blds | array.add b }}{{ end }}{{ end }}{{ cost_blds = cost_blds | array.add_range cost_x }}{{ cost_dev = [] }}{{ include 'partials/book-building-cost' }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Buildings = [C("A", 1), C("X", 0, false), C("B", 2)] }] }, CancellationToken.None);

        Regex.Matches(html, "<tr class=\"bh\"><td colspan=\"9\" style=\"font-weight:700;\">(.*?)</td>").Select(m => m.Groups[1].Value)
            .Should().Equal("สิ่งปลูกสร้างรายการที่ 1 : A", "สิ่งปลูกสร้างรายการที่ 2 : B", "สิ่งปลูกสร้าง (ไม่ประเมินมูลค่า) : X");
    }

    private static BuildingSection WithDev(BuildingSection b, params DevelopmentItem[] items) => new()
    {
        HouseNumber = b.HouseNumber, BuildingTypeText = b.BuildingTypeText, NumberOfFloors = b.NumberOfFloors, UsableArea = b.UsableArea,
        IsAppraisable = b.IsAppraisable, NotAppraisableReason = b.NotAppraisableReason, DevelopmentItems = items
    };

    private static DevelopmentItem D(string type, decimal area, bool? owner = null, string? reason = null) =>
        new() { BuildingTypeText = type, Area = area, IsAppraisable = owner, NotAppraisableReason = reason };

    [Fact]
    public async Task A_development_item_of_a_not_appraised_owner_leaves_the_table_and_goes_into_the_note_after_the_building_lines()
    {
        var s = await Section32Async(
            WithDev(B("10/1", 100m), D("รั้ว", 12m)),
            WithDev(B("10/2", 40m, false, "ชั่วคราว"), D("บ้านเดี่ยว", 77.77m, false, "ชั่วคราว")));

        s.Should().Contain("จำนวน 1 รายการ และส่วนพัฒนาอื่นๆ บนที่ดิน ดังนี้").And.Contain("<td>รั้ว</td>").And.Contain("12.00")
            .And.NotContain("<td class=\"r\">77.77</td>");
        s.Should().Contain("หมายเหตุ : ผู้ประเมินพิจารณาไม่ประเมินมูลค่าสิ่งปลูกสร้าง ดังนี้")
            .And.Contain("1. บ้านเดี่ยว 2 ชั้น บ้านเลขที่ 10/2 พื้นที่ใช้สอย 40.00 ตร.ม. เนื่องจากชั่วคราว</div>")
            .And.Contain("<div class=\"bk-p\" style=\"padding-left:32px;\">2. บ้านเดี่ยว พื้นที่ 77.77 ตร.ม. เนื่องจากชั่วคราว</div>");
        s.IndexOf("พื้นที่ใช้สอย 40.00", StringComparison.Ordinal).Should().BeLessThan(s.IndexOf("พื้นที่ 77.77", StringComparison.Ordinal));
    }

    [Fact]
    public async Task When_every_development_item_is_excluded_the_sub_row_and_the_intro_phrase_go_away_and_a_lone_line_has_no_lead()
    {
        var s = await Section32Async(WithDev(B("10/1", 100m), D("บ้านเดี่ยว", 77.77m, false, null)));

        s.Should().NotContain("ส่วนพัฒนาอื่นๆ บนที่ดิน").And.Contain("จำนวน 1 รายการ ดังนี้")
            .And.Contain("<p class=\"bk-p\">หมายเหตุ : ผู้ประเมินพิจารณาไม่ประเมินมูลค่าบ้านเดี่ยว พื้นที่ 77.77 ตร.ม.</p>").And.NotContain("เนื่องจาก");
    }

    [Fact]
    public async Task The_real_loader_maps_a_null_or_dash_reason_to_nothing_so_no_because_part_prints()
    {
        var id1 = Guid.NewGuid(); var id2 = Guid.NewGuid();
        DataTable T(params (string, Type)[] cols) { var t = new DataTable(); foreach (var (n, ty) in cols) t.Columns.Add(n, ty); return t; }
        var header = T(("BuildingDetailId", typeof(Guid)), ("GroupNumber", typeof(int)), ("HouseNumber", typeof(string)), ("IsAppraisable", typeof(bool)),
            ("NotAppraisableReason", typeof(string)), ("BuildingTypeText", typeof(string)), ("UsableArea", typeof(decimal)));
        header.Rows.Add(id1, 1, "3/48", false, DBNull.Value, "บ้านเดี่ยว", 33m);
        header.Rows.Add(id2, 1, "3/49", false, "-", "บ้านเดี่ยว", DBNull.Value);
        var dep = T(("DetailId", typeof(Guid)), ("BuildingAppraisalDetailId", typeof(Guid)), ("AreaDescription", typeof(string)), ("Area", typeof(decimal)),
            ("PricePerSqMBeforeDepreciation", typeof(decimal)), ("PriceBeforeDepreciation", typeof(decimal)), ("AgeYears", typeof(short)), ("DepreciationYearPct", typeof(decimal)),
            ("TotalDepreciationPct", typeof(decimal)), ("DepreciationAmount", typeof(decimal)), ("PriceAfterDepreciation", typeof(decimal)),
            ("PricePerSqMAfterDepreciation", typeof(decimal)), ("IsBuilding", typeof(bool)));
        dep.Rows.Add(Guid.NewGuid(), id2, "ลานจอดรถ", 80m, 0m, 0m, (short)0, 0m, 0m, 0m, 0m, 0m, false);
        var sets = new DataSet();
        sets.Tables.Add(header); sets.Tables.Add(dep);
        sets.Tables.Add(T(("Group", typeof(string)), ("Code", typeof(string)), ("Description", typeof(string))));
        sets.Tables.Add(T(("BuildingAppraisalDetailId", typeof(Guid)), ("FromFloorNumber", typeof(int)), ("ToFloorNumber", typeof(int))));
        sets.Tables.Add(T(("BuildingDetailId", typeof(Guid)), ("FilePath", typeof(string))));
        sets.Tables.Add(T(("DetailId", typeof(Guid)), ("AtYear", typeof(int)), ("ToYear", typeof(int)), ("DepreciationPerYear", typeof(decimal))));
        var factory = new FakeSqlConnectionFactory();
        factory.SetMultiResult(sets);

        var buildings = await BuildingSectionLoader.LoadAllAsync(factory.GetOpenConnection(), Guid.NewGuid());
        var html = await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(
            "{{ include 'partials/book-flow-standard' }}",
            new AppraisalSummaryModel { PropertyGroups = AppraisalBookDataProvider.BuildPropertyGroups([], buildings) }, CancellationToken.None);

        buildings.Select(b => b.NotAppraisableReason).Should().AllSatisfy(r => r.Should().BeNull());
        var note = html[html.IndexOf("หมายเหตุ : ผู้ประเมิน", StringComparison.Ordinal)..];
        note = note[..note.IndexOf("<div class=\"bk-bld\">", StringComparison.Ordinal)];
        note.Should().Contain("1. บ้านเดี่ยว บ้านเลขที่ 3/48 พื้นที่ใช้สอย 33.00 ตร.ม.</div>").And.Contain("2. บ้านเดี่ยว พื้นที่ 80.00 ตร.ม.</div>").And.NotContain("เนื่องจาก");
    }

    [Fact]
    public async Task The_development_cost_section_marks_a_not_appraised_property_after_the_appraised_one()
    {
        BuildingSection C(string type, bool? ap = null) => new()
        {
            BuildingTypeText = type, IsAppraisable = ap, IsDevelopmentOnly = true, CostRows = [new BuildingCostRow { AreaItemName = "x", Area = 1m }]
        };
        var html = await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(
            "{{ cost_blds = []; cost_dev = model.all_buildings }}{{ include 'partials/book-building-cost' }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Buildings = [C("รั้ว"), C("บ้านเดี่ยว", false)] }] }, CancellationToken.None);

        Regex.Matches(html, "<tr class=\"bh\"><td colspan=\"9\" style=\"font-weight:700;\">(.*?)</td>").Select(m => m.Groups[1].Value)
            .Should().Contain("บ้านเดี่ยว (ไม่ประเมินมูลค่า)").And.Contain("รั้ว");
    }
}
