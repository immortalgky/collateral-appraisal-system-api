using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers.Sections;
using Reporting.Infrastructure.Rendering;
using MappingRow = Reporting.Application.Providers.Sections.HypothesisSectionLoader.MappingRow;
using OtherCostRow = Reporting.Application.Providers.Sections.HypothesisSectionLoader.OtherCostRow;

namespace Reporting.Tests;

/// <summary>
/// The Hypothesis book page: the summary ledger follows the pricing screen band by band (L&amp;B and
/// condo), an empty field prints "-", and the unit details print only when units were uploaded.
/// </summary>
public class HypothesisSectionTests
{
    private static readonly PickedMethod Method = new()
    {
        GroupNumber = 1,
        Context = new PricingMethodContext { ApproachLabel = "วิธีรายได้", Remark = "ราคาขายอ้างอิงโครงการคู่แข่ง" },
    };

    private static Dictionary<string, object> Header(int variant, params (string Col, object Val)[] cols)
    {
        var h = new Dictionary<string, object> { ["Variant"] = variant };
        foreach (var (col, val) in cols) h[col] = val;
        return h;
    }

    private static HypothesisUnitRow Lb(int seq, string model, decimal land, decimal price) =>
        new() { Sequence = seq, Model = model, PlanNo = "123/45", LandAreaSqWa = land, UsableAreaSqM = 160m, SellingPrice = price };

    private static HypothesisSection LandBuilding() => HypothesisSectionLoader.Build(
        Method,
        Header(1,
            ("LandBuildingSummary_TotalArea", 1465m),
            ("LandBuildingSummary_TotalRevenue", 15_000_000m),
            ("LandBuildingSummary_TotalProjectDevCost", 20_000_000m),
            ("LandBuildingSummary_PublicUtilityRatePerSqWa", 1500m),
            ("LandBuildingSummary_EstimatedDurationMonths", 15m),
            ("LandBuildingSummary_DiscountRateFactor", 0.9123456m),
            ("LandBuildingSummary_TotalAssetValueRounded", 5_940_000m),
            ("LandBuildingSummary_Remark", "ที่ดินติดถนนสาธารณะ"),
            ("IndicatedValue", 6_000_000m)),
        [Lb(1, "Aspen", 50m, 4_000_000m), Lb(2, " aspen ", 52m, 5_000_000m), Lb(3, "Birch", 60m, 6_000_000m)],
        [new OtherCostRow { Category = 2, Description = "ค่ารื้อถอน", Amount = 100_000m, CategoryRatio = 0.5m }],
        [
            new MappingRow { ModelName = "ASPEN", AppraisalPropertyId = Guid.NewGuid(), BuildingName = "บ้านตัวอย่าง", FinalCostValue = 2_000_000m },
            new MappingRow { ModelName = "Birch", TotalCost = 9_000_000m },
        ]);

    private static HypothesisLedgerRow RowOf(HypothesisSection s, string band, string labelStart) =>
        s.Bands.Single(b => b.Title == band).Rows.First(r => r.Label.StartsWith(labelStart, StringComparison.Ordinal));

    [Fact]
    public void Land_building_ledger_follows_the_screen_bands_and_prints_every_cell()
    {
        var s = LandBuilding();

        s.HasRatio.Should().BeTrue();
        s.Bands.Select(b => b.Title).Should().Equal(
            "รายละเอียดพื้นที่ดินที่ประเมิน", "ประมาณการรายได้โครงการ", "ประมาณการระยะเวลาขาย",
            "ประมาณการต้นทุนพัฒนาโครงการ", "ประมาณการระยะเวลาก่อสร้าง", "ประมาณการต้นทุนโครงการ",
            "ภาษีและค่าธรรมเนียมรัฐ", "ค่าความเสี่ยง", "รวมต้นทุนและค่าใช้จ่ายในการพัฒนา", "มูลค่าทรัพย์สินสุดท้าย");

        RowOf(s, "รายละเอียดพื้นที่ดินที่ประเมิน", "พื้นที่รวม").Qty.Should().Be("1,465.00 ตร.วา");
        RowOf(s, "รายละเอียดพื้นที่ดินที่ประเมิน", "หมายเหตุ").Rate.Should().Be("ที่ดินติดถนนสาธารณะ");
        // A stored-but-empty input prints "-", a cell the row does not use stays null (blank).
        var utility = RowOf(s, "ประมาณการต้นทุนพัฒนาโครงการ", "ค่าก่อสร้างสาธารณูปโภค");
        utility.Rate.Should().Be("1,500.00 บาท/ตร.วา");
        utility.Total.Should().Be("-");
        RowOf(s, "ประมาณการต้นทุนพัฒนาโครงการ", "ค่ารื้อถอน").Total.Should().Be("100,000.00");
        RowOf(s, "ประมาณการต้นทุนพัฒนาโครงการ", "ค่ารื้อถอน").Rate.Should().BeNull();

        // Per house model: mapped building value × units, or the typed-over total (FSD C19–C21).
        var aspen = RowOf(s, "ประมาณการต้นทุนพัฒนาโครงการ", "- Aspen");
        aspen.Label.Should().Be("- Aspen · บ้านตัวอย่าง");
        aspen.Rate.Should().Be("2,000,000.00 บาท/หน่วย");
        aspen.Total.Should().Be("4,000,000.00");
        aspen.Ratio.Should().Be("20.00 %");
        // Typed-over total, no building: both marks, and the unset per-house cost prints "-" not "0.00".
        var birch = RowOf(s, "ประมาณการต้นทุนพัฒนาโครงการ", "- Birch");
        birch.Label.Should().Be("- Birch (แก้ยอดรวมเอง) (ยังไม่เลือกอาคาร)");
        birch.Rate.Should().Be("-");
        birch.Total.Should().Be("9,000,000.00");

        RowOf(s, "มูลค่าทรัพย์สินสุดท้าย", "ปัจจัยคิดลด").Rate.Should().Be("0.9123 (ตามระยะขาย 1.25 ปี (15 เดือน))");
        var final = RowOf(s, "มูลค่าทรัพย์สินสุดท้าย", "มูลค่าตามวิธี");
        final.Total.Should().Be("6,000,000.00");
        final.Rate.Should().Be("แก้เอง · ต่างจากค่าที่คำนวณ +60,000.00");
    }

    [Fact]
    public void A_real_zero_rate_with_a_zero_total_stays_zero()
    {
        var s = HypothesisSectionLoader.Build(Method,
            Header(1, ("LandBuildingSummary_PublicUtilityRatePerSqWa", 0m), ("LandBuildingSummary_PublicUtilityCost", 0m)),
            [Lb(1, "Cedar", 70m, 7_000_000m)], [], []);

        var cedar = RowOf(s, "ประมาณการต้นทุนพัฒนาโครงการ", "- Cedar");
        cedar.Label.Should().Be("- Cedar (ยังไม่เลือกอาคาร)");
        cedar.Rate.Should().Be("0.00 บาท/หน่วย");
        cedar.Total.Should().Be("0.00");
        RowOf(s, "ประมาณการต้นทุนพัฒนาโครงการ", "ค่าก่อสร้างสาธารณูปโภค").Rate.Should().Be("0.00 บาท/ตร.วา");
    }

    [Fact]
    public void Units_group_by_model_case_insensitively_with_a_total()
    {
        var s = LandBuilding();

        s.Models.Select(m => (m.Name, m.Units)).Should().Equal(("Aspen", 2), ("Birch", 1));
        s.Models[0].AvgLandAreaSqWa.Should().Be(51m);
        s.Models[0].AvgSellingPrice.Should().Be(4_500_000m);
        s.UnitTotal!.Units.Should().Be(3);
        s.UnitTotal.TotalSellingPrice.Should().Be(15_000_000m);
    }

    [Fact]
    public void Condo_ledger_has_its_own_bands_and_no_ratio_column()
    {
        var s = HypothesisSectionLoader.Build(Method,
            Header(2, ("CondominiumSummary_FAR", 40m), ("CondominiumSummary_TotalAssetValuePerSqM", 61_200m),
                ("CondominiumSummary_TotalAssetValueRounded", 181_200_000m)),
            [new HypothesisUnitRow { Sequence = 1, Model = "1BR", UsableAreaSqM = 30m, SellingPrice = 3_000_000m }], [], []);

        s.Variant.Should().Be(2);
        s.HasRatio.Should().BeFalse();
        s.Bands.Should().HaveCount(10);
        s.Bands.SelectMany(b => b.Rows).Should().OnlyContain(r => r.Ratio == null);
        RowOf(s, "รายละเอียดพื้นที่ดิน", "อัตราส่วนพื้นที่อาคารต่อที่ดิน").Rate.Should().Be("40 : 1");
        RowOf(s, "มูลค่าคงเหลือของทรัพย์สินทั้งหมดเป็นเงินสด", "มูลค่าต่อตารางเมตร").Total.Should().Be("61,200.00 บาท/ตร.ม.");
        RowOf(s, "มูลค่าคงเหลือของทรัพย์สินทั้งหมดเป็นเงินสด", "มูลค่าตามวิธี").Total.Should().Be("181,200,000.00");
        RowOf(s, "มูลค่าคงเหลือของทรัพย์สินทั้งหมดเป็นเงินสด", "มูลค่าตามวิธี").Rate.Should().BeNull();
    }

    [Fact]
    public void Empty_analysis_prints_dashes_and_no_unit_tables()
    {
        var s = HypothesisSectionLoader.Build(Method, Header(1), [], [], []);

        s.Models.Should().BeEmpty();
        s.UnitTotal.Should().BeNull();
        s.Bands.SelectMany(b => b.Rows).SelectMany(r => new[] { r.Rate, r.Qty, r.Total, r.Ratio })
            .Where(c => c != null).Should().OnlyContain(c => c == "-" || c == "รายได้ − ต้นทุนรวม");
    }

    private static Task<string> RenderAsync(HypothesisSection s) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(
            "{{ hyp = model.hypothesis_sections[0] }}{{ include 'partials/section-hypothesis' }}",
            new AppraisalSummaryModel { HypothesisSections = [s] }, CancellationToken.None);

    [Fact]
    public async Task Renders_ledger_units_and_method_remark()
    {
        var html = await RenderAsync(LandBuilding());

        html.Should().Contain("ประมาณการต้นทุนพัฒนาโครงการ").And.Contain("- Aspen &#183; บ้านตัวอย่าง")
            .And.Contain("<th style=\"width:9%;\">สัดส่วน</th>")
            .And.Contain("รายการหน่วย (3 หน่วย)").And.Contain("3 หลัง")
            .And.Contain("หมายเหตุและสมมติฐาน :").And.Contain("ราคาขายอ้างอิงโครงการคู่แข่ง")
            .And.Contain("<strong>หมายเหตุ :</strong> ที่ดินติดถนนสาธารณะ");
    }

    [Fact]
    public async Task Renders_condo_without_ratio_and_skips_empty_unit_tables()
    {
        var html = await RenderAsync(HypothesisSectionLoader.Build(
            new PickedMethod(), Header(2), [], [], []));

        html.Should().Contain("สรุปการคำนวณ — โครงการคอนโดมิเนียม").And.NotContain("สัดส่วน")
            .And.NotContain("รายการหน่วย").And.NotContain("รายละเอียดหน่วย").And.NotContain("หมายเหตุและสมมติฐาน");
    }
}
