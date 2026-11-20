using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>Section 6 "สรุปมูลค่าทรัพย์สินโดยวิธีต้นทุน": area, land value, buildings, the land + building sum (only when the final figure is rounded away from it) and the total.</summary>
public class BookCostSummaryTests
{
    private const string Template =
        "{{ include 'partials/book-funcs' }}{{ grp = model.property_groups[0]; ref_pass = false; n6 = 0; any6 = false; h6 = '6.'; multi6 = false }}{{ include 'partials/book-flow-methods' }}";

    private static BuildingSection Cost(bool? appraisable = null, bool devOnly = false) => new()
    {
        IsAppraisable = appraisable, IsDevelopmentOnly = devOnly,
        CostRows = [new BuildingCostRow { AreaItemName = "x", Area = 1m, ValueAfterDepreciation = 1m }], TotalValueAfterDepreciation = 1m, CostRowsTotal = 1m
    };

    private static async Task<string> RenderAsync(SummaryGroupRow group, params BuildingSection[] buildings)
    {
        var html = await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(Template,
            new AppraisalSummaryModel
            {
                ApproachValues = [new ApproachValueRow { GroupNumber = 1, ApproachType = "Cost" }],
                PropertyGroups = [new PropertyGroupDetail { GroupNumber = 1, Buildings = buildings }],
                Groups = [group]
            }, CancellationToken.None);
        var start = html.IndexOf("class=\"bk-sum\"", StringComparison.Ordinal);
        return html[start..];
    }

    // 2 ไร่ 2 งาน 66 วา = 1,066 wa at 5,000 baht per wa = 5,330,000; the building 6,123,000; their sum 11,453,000 is rounded to 11,450,000.
    private static SummaryGroupRow Rounded() => new()
    {
        GroupNumber = 1, TotalSquareWa = 1066m, LandUnitPrice = 5_000m, LandValue = 5_330_000m, LandSubtotal = 5_330_000m,
        BuildingSubtotal = 6_123_000m, GroupTotal = 11_450_000m
    };

    [Fact]
    public async Task Land_and_building_with_a_rounded_total_print_all_five_rows_in_order()
    {
        var s = await RenderAsync(Rounded(), Cost(), Cost(), Cost());

        var rows = new[]
        {
            "เนื้อที่ดินที่ประเมินมูลค่า 2 ไร่ 2 งาน 66 ตร.ว. (หรือ 1,066 ตารางวา)",
            "<span>ตารางวาละ 5,000.00 บาท</span><span>เป็นเงิน</span><span>5,330,000</span>",
            "<span>สิ่งปลูกสร้าง จำนวน 3 รายการ (ปัดเศษ)</span><span>เป็นเงิน</span><span>6,123,000</span>",
            "<div class=\"g\"><span>รวมมูลค่าที่ดิน พร้อมสิ่งปลูกสร้าง ตามสภาพปัจจุบัน</span><span>เป็นเงิน</span><span>11,453,000</span>",
            "<div class=\"b\"><span>รวมมูลค่าทรัพย์สิน โดยวิธีต้นทุน ตามสภาพปัจจุบัน (ปัดเศษ)</span><span>เป็นเงิน</span><span>11,450,000</span>"
        };
        rows.Select(r => s.IndexOf(r, StringComparison.Ordinal)).Should().BeInAscendingOrder().And.NotContain(-1);
    }

    [Fact]
    public async Task Without_a_rounding_difference_the_sum_row_and_the_rounding_note_are_absent()
    {
        var group = new SummaryGroupRow
        {
            GroupNumber = 1, TotalSquareWa = 1066m, LandUnitPrice = 5_000m, LandValue = 5_330_000m, LandSubtotal = 5_330_000m,
            BuildingOverrideValue = 6_000_000m, GroupTotal = 11_330_000m
        };

        var s = await RenderAsync(group, Cost());

        s.Should().NotContain("class=\"g\"").And.NotContain("รวมมูลค่าที่ดิน พร้อมสิ่งปลูกสร้าง")
            .And.Contain("<div class=\"b\"><span>รวมมูลค่าทรัพย์สิน โดยวิธีต้นทุน ตามสภาพปัจจุบัน</span>").And.NotContain("(ปัดเศษ)");   // an override has no "(ปัดเศษ)" either
        s.Should().Contain("<span>สิ่งปลูกสร้าง จำนวน 1 รายการ</span>");
    }

    [Fact]
    public async Task An_overridden_land_value_says_so_and_without_a_rate_it_is_plain()
    {
        var overridden = await RenderAsync(new SummaryGroupRow { GroupNumber = 1, TotalSquareWa = 100m, LandUnitPrice = 5_000m, LandValueOverridden = true, LandSubtotal = 700_000m, BuildingSubtotal = 1_000_000m, GroupTotal = 1_700_000m }, Cost());
        var noRate = await RenderAsync(new SummaryGroupRow { GroupNumber = 1, LandSubtotal = 700_000m, BuildingSubtotal = 1_000_000m, GroupTotal = 1_700_000m }, Cost());

        overridden.Should().Contain("<span>มูลค่าที่ดิน (ผู้ประเมินกำหนด)</span>").And.NotContain("ตารางวาละ");
        noRate.Should().Contain("<span>มูลค่าที่ดิน</span>").And.NotContain("ตารางวาละ").And.NotContain("เนื้อที่ดินที่ประเมินมูลค่า");   // no area: no area row
    }

    [Fact]
    public async Task A_rounded_land_product_keeps_the_equals_and_rounding_note()
    {
        var s = await RenderAsync(new SummaryGroupRow { GroupNumber = 1, TotalSquareWa = 100m, LandUnitPrice = 5_000m, LandValue = 500_000m, LandSubtotal = 480_000m, GroupTotal = 480_000m }, Cost());

        s.Should().Contain("ตารางวาละ 5,000.00 บาท = 500,000 (ปัดเศษ)");
    }

    [Fact]
    public async Task A_not_appraised_building_is_left_out_of_the_count()
    {
        var s = await RenderAsync(Rounded(), Cost(), Cost(), Cost(appraisable: false));

        s.Should().Contain("สิ่งปลูกสร้าง จำนวน 2 รายการ").And.NotContain("จำนวน 3 รายการ");
    }

    [Fact]
    public async Task An_appraised_development_property_adds_the_improvements_wording_and_a_not_appraised_one_does_not()
    {
        var withDev = await RenderAsync(Rounded(), Cost(), Cost(devOnly: true));
        var excludedDev = await RenderAsync(Rounded(), Cost(), Cost(devOnly: true, appraisable: false));

        withDev.Should().Contain("สิ่งปลูกสร้าง จำนวน 1 รายการ และส่วนปรับปรุงพัฒนาอื่นๆ");
        excludedDev.Should().Contain("สิ่งปลูกสร้าง จำนวน 1 รายการ (ปัดเศษ)").And.NotContain("ส่วนปรับปรุงพัฒนาอื่นๆ");
    }

    [Fact]
    public async Task A_land_only_group_drops_the_building_and_sum_rows_and_a_building_only_group_drops_the_land_rows()
    {
        var landOnly = await RenderAsync(new SummaryGroupRow { GroupNumber = 1, TotalSquareWa = 400m, LandUnitPrice = 1_000m, LandValue = 400_000m, LandSubtotal = 400_000m, GroupTotal = 400_000m }, Cost());
        var buildingOnly = await RenderAsync(new SummaryGroupRow { GroupNumber = 1, BuildingSubtotal = 900_000m, GroupTotal = 900_000m }, Cost());

        landOnly.Should().NotContain("สิ่งปลูกสร้าง จำนวน").And.NotContain("class=\"g\"").And.Contain("เนื้อที่ดินที่ประเมินมูลค่า 1 ไร่ 0 งาน 0 ตร.ว. (หรือ 400 ตารางวา)");
        buildingOnly.Should().NotContain("เนื้อที่ดิน").And.NotContain("ตารางวาละ").And.NotContain("มูลค่าที่ดิน").And.Contain("สิ่งปลูกสร้าง จำนวน 1 รายการ").And.NotContain("class=\"g\"");
    }
}
