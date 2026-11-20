using FluentAssertions;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers.Sections;
using CalcRow = Reporting.Application.Providers.Sections.SaleGridSectionLoader.CalcRow;
using CompFactorRow = Reporting.Application.Providers.Sections.SaleGridSectionLoader.CompFactorRow;
using FactorScoreRow = Reporting.Application.Providers.Sections.SaleGridSectionLoader.FactorScoreRow;
using EavRow = Reporting.Application.Providers.Sections.SaleGridSectionLoader.EavRow;

namespace Reporting.Tests;

/// <summary>WQS / Sale Grid / Direct Comparison tables: the cells derived from stored columns, the SG/DC split, final block.</summary>
public class PricingGridTests
{
    // ── Derived cells (mirror the screen's formulas) ───────────────────────────

    [Fact]
    public void Adjusted_value_offer_pct_wins_over_amount_like_the_screen()
    {
        var c = new CalcRow { OfferingPrice = 60000m, AdjustOfferPricePct = 5m, AdjustOfferPriceAmt = 1m };
        SaleGridSectionLoader.AdjustedValue(c).Should().Be(57000m);
    }

    [Fact]
    public void Adjusted_value_offer_amount_is_used_when_pct_is_zero_and_price_otherwise()
    {
        SaleGridSectionLoader.AdjustedValue(new CalcRow { OfferingPrice = 90000m, AdjustOfferPricePct = 0m, AdjustOfferPriceAmt = 85000m }).Should().Be(85000m);
        SaleGridSectionLoader.AdjustedValue(new CalcRow { OfferingPrice = 90000m, AdjustOfferPricePct = 0m, AdjustOfferPriceAmt = 0m }).Should().Be(90000m);
    }

    [Fact]
    public void Adjusted_value_sale_price_grows_by_years_times_period_pct()
    {
        // 80,000 x (1 + 36 x 3 / 100) = 166,400 (69000150, market 2)
        SaleGridSectionLoader.AdjustedValue(new CalcRow { SellingPrice = 80000m, BuySellYear = 36, AdjustedPeriodPct = 3m }).Should().Be(166400m);
        SaleGridSectionLoader.AdjustedValue(new CalcRow()).Should().BeNull();
    }

    [Fact]
    public void After_second_revision_adds_land_and_building_compensation()
    {
        var c = new CalcRow { OfferingPrice = 100000m, LandValueAdjustment = 1600m, BuildingValueAdjustment = -150m };
        SaleGridSectionLoader.AfterSecondRevision(c).Should().Be(101450m);
    }

    [Fact]
    public void Wqs_weighted_intensity_is_weight_times_intensity_and_totals_sum_the_columns()
    {
        WqsSectionLoader.WeightedIntensity(2m, 10m).Should().Be("20.00");
        WqsSectionLoader.WeightedIntensity(null, null).Should().BeNull();

        Guid f1 = Guid.NewGuid(), f2 = Guid.NewGuid(), m1 = Guid.NewGuid();
        WqsSectionLoader.ScoreRow Row(Guid f, Guid? m, decimal? w, decimal? i, decimal s, decimal ws) =>
            new() { FactorId = f, MarketComparableId = m, FactorWeight = w, Intensity = i, Score = s, WeightedScore = ws };
        var rows = new[]
        {
            Row(f1, null, 2m, 10m, 9m, 18m), Row(f2, null, 1m, 10m, 9m, 9m),
            Row(f1, m1, null, null, 8m, 16m),  Row(f2, m1, null, null, 8m, 8m),
        };

        var t = WqsSectionLoader.TotalsRow([f1, f2], rows, [m1]);
        t.Weight.Should().Be("3");
        t.Intensity.Should().Be("20");
        t.WeightedIntensity.Should().Be("30");
        t.SubjectScore.Should().Be("18");
        t.SubjectWeighted.Should().Be("27.00");
        t.ComparableScores.Should().Equal("16");
        t.ComparableWeighted.Should().Equal("24.00");
    }

    // ── SG / DC rows ───────────────────────────────────────────────────────────

    private static IReadOnlyList<SaleGridRow> Rows(bool dc, bool screenWeighted = false)
    {
        Guid c1 = Guid.NewGuid(), c2 = Guid.NewGuid(), f = Guid.NewGuid();
        var calc = new Dictionary<Guid, CalcRow>
        {
            [c1] = new() { MarketComparableId = c1, OfferingPrice = 70000m, TotalAdjustedValue = 73500m, Weight = 0.33333m, WeightedAdjustedValue = 24500m },
            [c2] = new() { MarketComparableId = c2, SellingPrice = 80000m, BuySellYear = 36, AdjustedPeriodPct = 3m, TotalAdjustedValue = 174720m },
        };
        var factor = new CompFactorRow { FactorId = f, FactorCode = "02", DataType = "Numeric", FactorName = "เนื้อที่ดิน (ไร่)" };
        return SaleGridSectionLoader.BuildRows(dc, [c1, c2], calc, [factor],
            [new EavRow { FactorId = f, MarketComparableId = c1, Value = "300" }],
            [new FactorScoreRow { FactorId = f, MarketComparableId = c1, ComparisonResult = "I", Remarks = "note" }],
            new Dictionary<string, IReadOnlyDictionary<string, string?>>(),
            new Dictionary<string, string?> { ["02"] = "912.33 ตารางวา" },
            showLand: true, showBuilding: false, finalValue: "114,240.00", finalLabel: dc ? "มูลค่าสุทธิ (ต่ำสุด)" : "มูลค่าสุทธิ (ถ่วงน้ำหนัก)",
            toApplicationTime: d => d, screenWeighted: screenWeighted);
    }

    [Fact]
    public void Direct_comparison_has_no_weight_band_and_sale_grid_has()
    {
        Rows(dc: true).Select(r => r.Label).Should().NotContain("ปรับน้ำหนัก").And.NotContain("มูลค่าที่ปรับแล้วถ่วงน้ำหนัก");
        Rows(dc: true).Last().Label.Should().Be("มูลค่าสุทธิ (ต่ำสุด)");

        var sg = Rows(dc: false);
        sg.Select(r => r.Label).Should().Contain(["ปรับน้ำหนัก", "ปัจจัยน้ำหนักความน่าเชื่อถือของข้อมูล", "มูลค่าที่ปรับแล้วถ่วงน้ำหนัก"]);
        sg.Last().Label.Should().Be("มูลค่าสุทธิ (ถ่วงน้ำหนัก)");
        sg.Last().SubjectValue.Should().Be("114,240.00");
    }

    [Fact]
    public void Per_area_weighted_row_is_the_screens_product_at_two_decimals_and_the_stored_value_otherwise()
    {
        // 73,500 × .33333 = 24,499.755 -> 24,499.76 on the screen; the save stored 24,500.00 from the unrounded weight.
        Rows(dc: false, screenWeighted: true).Single(r => r.Label == "มูลค่าที่ปรับแล้วถ่วงน้ำหนัก").Values.Should().Equal("24,499.76", null);
        Rows(dc: false).Single(r => r.Label == "มูลค่าที่ปรับแล้วถ่วงน้ำหนัก").Values.Should().Equal("24,500.00", null);
    }

    [Fact]
    public void Years_print_BuySellYear_not_a_years_and_months_text()
    {
        var years = Rows(dc: false).Single(r => r.Label == "จำนวนปี");
        years.Values.Should().Equal(null, "36");
    }

    [Fact]
    public void Factor_row_has_collateral_value_comparable_value_level_and_remark()
    {
        var rows = Rows(dc: false);
        var f = rows.First(r => r.Label == "เนื้อที่ดิน (ไร่)");
        f.SubjectValue.Should().Be("912.33");
        f.Values.Should().Equal("300.00", null);
        f.Amounts.Should().Equal("ด้อยกว่า", null);

        var adjust = rows.Last(r => r.Label == "เนื้อที่ดิน (ไร่)");
        adjust.SubjectValue.Should().Be("note");
    }

    [Fact]
    public void Derived_cells_show_in_the_adjusted_and_second_revision_rows()
    {
        var rows = Rows(dc: false);
        rows.Single(r => r.Label == "มูลค่าที่ปรับแล้ว").Values.Should().Equal("70,000.00", "166,400.00");
        rows.Single(r => r.Label == "รวมหลังปรับครั้งที่ 2").Values.Should().Equal("70,000.00", "166,400.00");
        rows.Select(r => r.Label).Should().Contain("พื้นที่ที่ดินส่วนขาด - เกิน (ตร.วา)").And.NotContain("พื้นที่ใช้สอยส่วนขาด - เกิน (ตร.ม.)");
    }

    [Theory]
    [InlineData("E", "เท่ากัน")]
    [InlineData("I", "ด้อยกว่า")]
    [InlineData("B", "ดีกว่า")]
    [InlineData(null, null)]
    public void Level_words(string? code, string? expected) => SaleGridSectionLoader.Level(code).Should().Be(expected);

    // ── Final block, headers, chunks ───────────────────────────────────────────

    [Fact]
    public void Final_block_uses_FinalValueOverride_for_the_rate_and_marks_a_manual_rate()
    {
        var fv = new PricingGridCommon.FinalValueRow { FinalValue = 65045.58m, FinalValueOverride = 64218.36m, LandArea = 800m, IndicatedValue = 51375000m };
        var rows = PricingGridCommon.FinalRows(fv, "PerSqWa", "มูลค่าสุทธิ (การถดถอย)", perUnitSuffix: false);

        rows.Select(r => r.Label).Should().Equal("มูลค่าสุทธิ (การถดถอย)", "ราคาต่อตร.วาที่ใช้", "เนื้อที่ดิน", "มูลค่าตามวิธี (Indicated Value)");
        rows[1].Value.Should().Be("64,218.36");
        rows[1].Note.Should().Be("แก้เอง · ต่างจากที่คำนวณ -827.22");
        rows[2].Value.Should().Be("800.00");
    }

    [Fact]
    public void Final_block_lump_sum_marks_a_manual_indicated_value_and_building_rows_follow_HasBuildingValue()
    {
        var lump = PricingGridCommon.FinalRows(new() { FinalValue = 34589000m, IndicatedValue = 34000000m }, "PerUnit", "มูลค่าสุทธิ (ถ่วงน้ำหนัก)", perUnitSuffix: true);
        lump.Select(r => r.Label).Should().Equal("มูลค่าสุทธิ (ถ่วงน้ำหนัก)", "มูลค่าตามวิธี (Indicated Value)");
        lump[1].Note.Should().Be("แก้เอง · ต่างจากที่คำนวณ -589,000.00");

        var withBuilding = PricingGridCommon.FinalRows(
            new() { FinalValue = 66500m, FinalValueOverride = 66500m, LandArea = 2389m, HasBuildingValue = true, LandValue = 158869000m, BuildingValue = 2970000m, IndicatedValue = 160000000m },
            "PerSqWa", "มูลค่าสุทธิ (การถดถอย)", perUnitSuffix: false);
        withBuilding.Select(r => r.Label).Should().Equal("มูลค่าสุทธิ (การถดถอย)", "ราคาต่อตร.วาที่ใช้", "เนื้อที่ดิน", "มูลค่าที่ดิน", "ค่าอาคาร จาก Building Cost", "มูลค่าตามวิธี (รวมอาคาร)");
        withBuilding[1].Note.Should().BeNull();
    }

    [Fact]
    public void Market_sub_line_and_balanced_chunks()
    {
        PricingGridCommon.MarketSub(null, null, new DateTime(2026, 3, 5), "PerSqWa", null).Should().Be("ประกาศขาย · 5 มีนาคม 2569 · /ตร.ว.");
        PricingGridCommon.MarketSub(1m, new DateTime(2024, 11, 1), null, null, "PerSqm").Should().Be("ซื้อขายจริง · 1 พฤศจิกายน 2567 · /ตร.ม.");
        PricingGridCommon.MarketSub(null, null, null, null, null).Should().Be("ประกาศขาย · - · เหมา");

        PricingGridCommon.Chunks(4, 3).Select(c => c.Count).Should().Equal(2, 2);
        PricingGridCommon.Chunks(5, 3).Select(c => (c.Start, c.Count)).Should().Equal((0, 3), (3, 2));
        PricingGridCommon.Chunks(3, 3).Should().ContainSingle();
        PricingGridCommon.Chunks(0, 3).Should().BeEmpty();
    }

    // ── Collateral column (ComparisonSubject) ──────────────────────────────────

    [Fact]
    public void Subject_maps_building_style_plot_location_and_condo_facilities()
    {
        var group = new PropertyGroupDetail
        {
            GroupNumber = 1,
            Lands = [new LandSection { PlotLocationType = "แปลงหัวมุม, ติดสโมสร" }],
            Buildings = [new BuildingSection { BuildingForm = "บ้านเดี่ยว 2 ชั้น" }]
        };
        var v = ComparisonSubject.Values(group, null, null, null);
        v["54"].Should().Be("บ้านเดี่ยว 2 ชั้น");
        v["52"].Should().Be("แปลงหัวมุม, ติดสโมสร");
        v.Should().NotContainKey("32");

        ComparisonSubject.Values(null, "ห้องชุด", null, new CondoGroupFigures { Facilities = "สระว่ายน้ำ" })["32"].Should().Be("สระว่ายน้ำ");
    }

    [Fact]
    public void Subject_cell_drops_the_unit_suffix_of_a_numeric_value()
    {
        var f = new CompFactorRow { FactorCode = "02", DataType = "Numeric" };
        SaleGridSectionLoader.Subject(f, new Dictionary<string, string?> { ["02"] = "912.33 ตารางวา" }).Should().Be("912.33");
        SaleGridSectionLoader.Subject(f, new Dictionary<string, string?>()).Should().BeNull();
    }
}
