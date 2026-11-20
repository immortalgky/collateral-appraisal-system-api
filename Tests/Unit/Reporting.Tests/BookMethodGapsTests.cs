using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers.Sections;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>
/// The method pages' gaps: market sub-analyses under their owner, growth / depreciation period tables,
/// Profit Rent totals, machine notes, the per-sqm usable area and the method remark box.
/// </summary>
public class BookMethodGapsTests
{
    private static Task<string> RenderAsync(string template, AppraisalSummaryModel model) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(template, model, CancellationToken.None);

    private static WqsSection Wqs(string remark = "อ้างอิงตลาด 3 แห่ง") => new()
    {
        Context = new PricingMethodContext { Remark = remark },
        FinalRows = [new PricingFinalRow { Label = "มูลค่าสุทธิ (การถดถอย)", Value = "1,234.00", Unit = "บาท" }]
    };

    private static PricingSubAnalysis Sub(string heading, string? caption = null) => new()
    {
        Heading = heading,
        Caption = caption,
        MethodLabel = PricingSubAnalysisLoader.MethodLabel("WQS"),
        Wqs = Wqs()
    };

    // ── Sub-analyses ──────────────────────────────────────────────────────────────

    [Fact]
    public void Sub_analysis_headings_follow_the_domain_subject_types()
    {
        PricingSubAnalysisLoader.Heading(2).Should().Be("ที่มาของราคาทดแทนใหม่ (RCN)");
        PricingSubAnalysisLoader.Heading(4).Should().Be("ที่มาของมูลค่าที่ดินต่อตารางวา");
        PricingSubAnalysisLoader.Heading(6).Should().Be("ที่มาของค่าเช่าตลาด");
        PricingSubAnalysisLoader.Heading(3).Should().Be("ที่มาของมูลค่าที่ดิน");
        PricingSubAnalysisLoader.Heading(5).Should().Be("ที่มาของค่าห้องพัก");
    }

    [Fact]
    public async Task Sub_analyses_print_the_heading_once_per_run_and_hide_the_nested_banner()
    {
        var model = new AppraisalSummaryModel { LeaseholdSections = [new LeaseholdSection { LandValueSources =
            [Sub("ที่มาของค่าห้องพัก", "ประเภทห้อง Standard"), Sub("ที่มาของค่าห้องพัก", "ประเภทห้อง Deluxe"), Sub("ที่มาของมูลค่าที่ดิน")] }] };

        var html = await RenderAsync("{{ subs = model.leasehold_sections[0].land_value_sources }}{{ include 'partials/book-sub-analyses' }}", model);

        CountOf(html, ">ที่มาของค่าห้องพัก<").Should().Be(1);
        CountOf(html, ">ที่มาของมูลค่าที่ดิน<").Should().Be(1);
        html.Should().Contain("ประเภทห้อง Deluxe · วิธีให้คะแนนคุณภาพถ่วงน้ำหนัก (WQS)")
            .And.Contain("1,234.00").And.Contain("หมายเหตุและสมมติฐาน").And.Contain(".sub-an .section-bar, .sub-an .method-banner { display:none; }");
    }

    [Fact]
    public async Task No_sub_analysis_prints_nothing()
    {
        var html = await RenderAsync("{{ subs = [] }}{{ include 'partials/book-sub-analyses' }}", new AppraisalSummaryModel());
        html.Trim().Should().BeEmpty();
    }

    // ── Leasehold / Profit Rent ──────────────────────────────────────────────────

    [Fact]
    public async Task Leasehold_prints_growth_periods_land_value_source_and_remark()
    {
        var model = new AppraisalSummaryModel { LeaseholdSections = [new LeaseholdSection
        {
            Context = new PricingMethodContext { Remark = "สัญญาเช่าคงเหลือ 22 ปี" },
            LandGrowthPeriods = [new GrowthPeriodRow { FromYear = 1, ToYear = 5, GrowthRatePercent = 3m }, new GrowthPeriodRow { FromYear = 6, ToYear = 10, GrowthRatePercent = 5.125m }],
            LandValueSources = [Sub(PricingSubAnalysisLoader.Heading(4))]
        }] };

        var html = await RenderAsync("{{ lh = model.leasehold_sections[0] }}{{ include 'partials/section-leasehold' }}", model);

        html.Should().Contain("ช่วงการเติบโตของมูลค่าที่ดิน").And.Contain("1 – 5").And.Contain("3.00").And.Contain("5.125")
            .And.Contain("ที่มาของมูลค่าที่ดินต่อตารางวา").And.Contain("สัญญาเช่าคงเหลือ 22 ปี");
    }

    [Fact]
    public async Task Leasehold_without_periods_sources_or_remark_prints_none_of_those_blocks()
    {
        var model = new AppraisalSummaryModel { LeaseholdSections = [new LeaseholdSection { Context = new PricingMethodContext() }] };

        var html = await RenderAsync("{{ lh = model.leasehold_sections[0] }}{{ include 'partials/section-leasehold' }}", model);

        html.Should().NotContain("ช่วงการเติบโตของมูลค่าที่ดิน").And.NotContain("ที่มาของ").And.NotContain("หมายเหตุและสมมติฐาน");
    }

    [Fact]
    public void Only_the_period_growth_type_loads_the_period_table()
    {
        LeaseholdSectionLoader.IsPeriod("Period").Should().BeTrue();
        LeaseholdSectionLoader.IsPeriod("Frequency").Should().BeFalse();
        LeaseholdSectionLoader.IsPeriod(null).Should().BeFalse();
    }

    [Fact]
    public async Task Profit_rent_prints_the_rent_totals_the_building_flag_growth_source_and_remark()
    {
        var model = new AppraisalSummaryModel { ProfitRentSections = [new ProfitRentSection
        {
            Context = new PricingMethodContext { Remark = "ค่าเช่าตลาดอ้างอิงอาคารใกล้เคียง" },
            TotalMarketRentalFee = 18_600_000m, TotalContractRentalFee = 12_400_000m, TotalReturnsFromLease = 6_200_000m,
            TotalPresentValue = 4_380_000m, IncludeBuildingCost = true,
            GrowthPeriods = [new GrowthPeriodRow { FromYear = 1, ToYear = 3, GrowthRatePercent = 10m }],
            MarketRentSources = [Sub(PricingSubAnalysisLoader.Heading(6))]
        }] };

        var html = await RenderAsync("{{ pr = model.profit_rent_sections[0] }}{{ include 'partials/section-profitrent' }}", model);

        html.Should().Contain("ค่าเช่าตลาดรวม").And.Contain("18,600,000.00").And.Contain("ค่าเช่าตามสัญญารวม").And.Contain("12,400,000.00")
            .And.Contain("ส่วนต่างค่าเช่ารวม (Profit Rent)").And.Contain("6,200,000.00")
            .And.Contain("รวมมูลค่าอาคาร :</span><span class=\"v\">รวม<")
            .And.Contain("ช่วงการเติบโตของค่าเช่าตลาด").And.Contain("ที่มาของค่าเช่าตลาด").And.Contain("ค่าเช่าตลาดอ้างอิงอาคารใกล้เคียง");
    }

    [Fact]
    public async Task Profit_rent_without_building_prints_not_included()
    {
        var model = new AppraisalSummaryModel { ProfitRentSections = [new ProfitRentSection()] };
        var html = await RenderAsync("{{ pr = model.profit_rent_sections[0] }}{{ include 'partials/section-profitrent' }}", model);
        html.Should().Contain("<span class=\"v\">ไม่รวม<").And.NotContain("ช่วงการเติบโตของค่าเช่าตลาด");
    }

    // ── Machinery ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Machine_cost_table_prints_notes_rcn_source_and_remark()
    {
        var model = new AppraisalSummaryModel { CostMachineSections = [new CostMachineSection { Groups = [new MachineCostGroup
        {
            Rows = [new MachineCostRow { Sequence = 1, MachineDetail = "เครื่องฉีดพลาสติก", Notes = "มีประวัติซ่อมใหญ่ปี 2025" }, new MachineCostRow { Sequence = 2 }],
            RcnSources = [Sub(PricingSubAnalysisLoader.Heading(2), "ลำดับที่ 1 เครื่องฉีดพลาสติก")],
            Remark = "ราคาทดแทนใหม่อ้างอิงใบเสนอราคา 2 ราย"
        }] }] };

        var html = await RenderAsync("{{ cms = model.cost_machine_sections[0] }}{{ include 'partials/section-costmachine' }}", model);

        html.Should().Contain(">หมายเหตุ</th>").And.Contain("มีประวัติซ่อมใหญ่ปี 2025")
            .And.Contain("ที่มาของราคาทดแทนใหม่ (RCN)").And.Contain("ลำดับที่ 1 เครื่องฉีดพลาสติก ·")
            .And.Contain("ราคาทดแทนใหม่อ้างอิงใบเสนอราคา 2 ราย");
    }

    [Fact]
    public async Task Machine_figures_never_wrap_and_the_notes_column_takes_the_wrapping()
    {
        var model = new AppraisalSummaryModel { CostMachineSections = [new CostMachineSection { Groups = [new MachineCostGroup
        {
            Rows = [new MachineCostRow { Sequence = 1, RemainingR = -4m, ConditionFactorC = 1m, MarketDemand = "ใช้งานไม่ได้", Notes = "หมายเหตุยาว" }]
        }] }] };

        var html = await RenderAsync("{{ cms = model.cost_machine_sections[0] }}{{ include 'partials/section-costmachine' }}", model);

        html.Should().Contain(".cm-cost-table td.nw { white-space:nowrap; }")
            .And.Contain("<td class=\"ctr nw\">-4.0</td>").And.Contain("<td class=\"ctr nw\">1.00</td>")
            .And.Contain("<td class=\"ctr nw\">ใช้งานไม่ได้</td>")
            .And.Contain("white-space:nowrap;\">ลำดับที่</th>").And.Contain("<td class=\"ctr nw\">รวม</td>").And.Contain("<td class=\"cm-note\">หมายเหตุยาว</td>");
    }

    // ── Building cost depreciation ───────────────────────────────────────────────

    [Fact]
    public void Per_year_rate_comes_from_the_periods_the_form_writes()
    {
        DepreciationPeriodRow P(int a, int t, decimal r) => new() { AtYear = a, ToYear = t, DepreciationPerYear = r };

        BuildingSectionLoader.PerYearPct("Gross", 0m, [P(1, 5, 2m)]).Should().Be(2m);             // เหมารวม: the one 1..age row
        BuildingSectionLoader.PerYearPct("Gross", 0m, [P(1, 5, 0m)]).Should().Be(0m);             // a real 0 stays 0
        BuildingSectionLoader.PerYearPct("Gross", 1.8m, []).Should().Be(1.8m);                    // the column when set
        BuildingSectionLoader.PerYearPct("Gross", 0m, []).Should().BeNull();                      // nothing stored
        BuildingSectionLoader.PerYearPct("Period", 0m, [P(1, 3, 1.5m), P(4, 5, 2.75m)]).Should().BeNull(); // รายช่วง: period table
        BuildingSectionLoader.DepreciationMethodLabel("Gross").Should().Be("เหมารวม");
        BuildingSectionLoader.DepreciationMethodLabel("Period").Should().Be("รายช่วง");
    }

    private const string CostSetup =
        "cost_blds = [{ building_number: 1, cost_method_remark: 'ราคาก่อสร้างตามสมาคมฯ', cost_rows: [" +
        "{ area_item_name: 'บ้านเดี่ยว', depreciation_pct_per_year: null, depreciation_method: 'รายช่วง', price_per_sqm_after_depreciation: 11480, " +
        "  periods: [{ at_year: 1, to_year: 10, depreciation_per_year: 1.5 }, { at_year: 11, to_year: 20, depreciation_per_year: 2 }] }," +
        "{ area_item_name: 'โรงจอดรถ', depreciation_pct_per_year: 0, depreciation_method: 'เหมารวม', price_per_sqm_after_depreciation: 5000, " +
        "  periods: [{ at_year: 1, to_year: 3, depreciation_per_year: 0 }] }] }]; cost_dev = []; ";

    [Fact]
    public async Task Building_cost_prints_the_per_year_cell_of_each_row_and_no_depreciation_method_table()
    {
        var html = await RenderAsync("{{ " + CostSetup + "cost_show_remark = true }}{{ include 'partials/book-building-cost' }}", new AppraisalSummaryModel());

        html.Should().Contain(">รายช่วง</td>")                     // the ปีละ cell of a รายช่วง row is no longer blank
            .And.Contain("ราคาก่อสร้างตามสมาคมฯ");
        html.Should().Contain("<td class=\"num\">0.00</td>"); // a real 0 % per year prints 0.00
        // the method / year-range / price-after-depreciation detail table is not printed on any page
        html.Should().NotContain("วิธีคิดค่าเสื่อม").And.NotContain("11 – 20").And.NotContain("11,480.00");
    }

    [Fact]
    public async Task Building_cost_remark_prints_only_on_the_valuation_page()
    {
        var html = await RenderAsync("{{ " + CostSetup + "cost_show_remark = false }}{{ include 'partials/book-building-cost' }}", new AppraisalSummaryModel());
        html.Should().NotContain("ราคาก่อสร้างตามสมาคมฯ");
    }

    private static BuildingSection BuildingWithDepreciationDetail() => new()
    {
        BuildingTypeText = "บ้านเดี่ยว",
        CostRows =
        [
            new BuildingCostRow
            {
                AreaItemName = "บ้านเดี่ยว", DepreciationMethod = "เหมารวม", PricePerSqmAfterDepreciation = 99_000m,
                Periods = [new DepreciationPeriodRow { AtYear = 1, ToYear = 10, DepreciationPerYear = 1m }]
            }
        ]
    };

    [Fact]
    public async Task No_page_prints_the_depreciation_method_table()
    {
        var model = new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Buildings = [BuildingWithDepreciationDetail()] }] };

        var buildingPage = await RenderAsync("{{ include 'partials/book-funcs' }}{{ b = model.all_buildings[0]; bld_k = 1 }}{{ include 'partials/book-building-block' }}", model);
        var valuationPage = await RenderAsync("{{ cost_blds = [model.all_buildings[0]]; cost_dev = [] }}{{ include 'partials/book-building-cost' }}", model);

        buildingPage.Should().NotContain("วิธีคิดค่าเสื่อม").And.NotContain("99,000.00");
        valuationPage.Should().Contain("การคิดค่าเสื่อมราคา").And.NotContain("วิธีคิดค่าเสื่อม").And.NotContain("99,000.00");
    }

    private static BuildingSection CostBuilding(decimal? keyed) => new()
    {
        BuildingTypeText = "บ้านเดี่ยว",
        CostRows = [new BuildingCostRow { AreaItemName = "บ้านเดี่ยว", Area = 80m, PricePerSqm = 100_000m, Price = 8_000_000m, ValueAfterDepreciation = 7_920_000m }],
        TotalValueAfterDepreciation = keyed ?? 7_920_000m,
        CostRowsTotal = 7_920_000m,
        CostOverride = keyed is { } k && k != 7_920_000m ? k : null
    };

    private static Task<string> RenderBuildingPageAsync(BuildingSection b) =>
        RenderAsync("{{ include 'partials/book-funcs' }}{{ b = model.all_buildings[0]; bld_k = 1 }}{{ include 'partials/book-building-block' }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Buildings = [b] }] });

    private static Task<string> RenderCombinedCostAsync(params BuildingSection[] buildings) =>
        RenderAsync("{{ cost_blds = model.all_buildings; cost_dev = [] }}{{ include 'partials/book-building-cost' }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Buildings = buildings }] });

    [Fact]
    public async Task The_building_page_has_no_cost_table_the_combined_cost_section_carries_it()
    {
        var html = await RenderBuildingPageAsync(CostBuilding(6_000_000m));

        html.Should().NotContain("รายการประเมินทรัพย์สินโดยวิธีต้นทุน").And.NotContain("ปรับโดยผู้ประเมิน").And.NotContain("<table class=\"grid\">");
    }

    [Fact]
    public async Task The_combined_table_closes_an_overridden_building_on_its_rows_total_then_prints_the_keyed_value_on_its_own_row()
    {
        var html = await RenderCombinedCostAsync(CostBuilding(6_000_000m));

        html.Split("7,920,000.00").Length.Should().Be(3); // the row and the รวม row
        html.Split("6,000,000.00").Length.Should().Be(2); // printed once, on the override row
        html.Split("ปรับโดยผู้ประเมิน").Length.Should().Be(2);
        html.IndexOf("ปรับโดยผู้ประเมิน", StringComparison.Ordinal).Should().BeGreaterThan(html.LastIndexOf("7,920,000.00", StringComparison.Ordinal));
        html.Should().NotContain("(คำนวณ)").And.NotContain("ตามสภาพปัจจุบัน");
    }

    [Fact]
    public async Task With_two_buildings_only_the_overridden_one_gets_an_override_row_right_under_its_own_total()
    {
        var html = await RenderCombinedCostAsync(CostBuilding(null), CostBuilding(6_000_000m));

        html.Split("ปรับโดยผู้ประเมิน").Length.Should().Be(2);
        html.Split("7,920,000.00").Length.Should().Be(5); // two buildings, a row and a รวม row each
        html.IndexOf("ปรับโดยผู้ประเมิน", StringComparison.Ordinal).Should().BeGreaterThan(html.IndexOf("<td>รวม</td>", html.IndexOf("<td>รวม</td>", StringComparison.Ordinal) + 1, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_combined_table_without_an_override_is_the_plain_total()
    {
        var html = await RenderCombinedCostAsync(CostBuilding(7_920_000m));

        html.Should().Contain("7,920,000.00").And.NotContain("ปรับโดยผู้ประเมิน").And.NotContain("(คำนวณ)").And.NotContain("ตามสภาพปัจจุบัน");
        html.Split("7,920,000.00").Length.Should().Be(3);
    }

    [Theory]
    [InlineData(7_920_000, 7_920_000)]
    [InlineData(7_918_499, 7_918_000)]
    [InlineData(7_918_500, 7_919_000)]
    public void The_rows_total_is_rounded_to_the_nearest_thousand_half_away(double sum, double expected)
        => BuildingSectionLoader.RoundedRowsTotal((decimal)sum).Should().Be((decimal)expected);

    private static BuildingSection DevProperty(string type, string row, int number = 0, decimal total = 0m) => new()
    {
        BuildingNumber = number, BuildingTypeText = type, IsDevelopmentOnly = true,
        CostRows = [new BuildingCostRow { AreaItemName = row, Area = 10m }],
        TotalArea = 10m, TotalPrice = total, TotalValueAfterDepreciation = total
    };

    [Fact]
    public async Task Each_development_property_gets_a_header_followed_by_its_own_rows()
    {
        var html = await RenderAsync(
            "{{ cost_blds = []; cost_dev = model.all_buildings }}{{ include 'partials/book-building-cost' }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Buildings = [DevProperty("บ้านเดี่ยว", "เนื้อที่ภายใน", total: 111_000m), DevProperty("ทาวน์เฮ้าส์", "ลานจอดรถ", total: 222_000m)] }] });

        var group = html.IndexOf("ส่วนพัฒนาอื่นๆ บนที่ดิน", StringComparison.Ordinal);
        var h1 = html.IndexOf("class=\"bh\"><td colspan=\"9\" style=\"font-weight:700;\">บ้านเดี่ยว</td>", StringComparison.Ordinal);
        var r1 = html.IndexOf("- เนื้อที่ภายใน", StringComparison.Ordinal);
        var h2 = html.IndexOf("class=\"bh\"><td colspan=\"9\" style=\"font-weight:700;\">ทาวน์เฮ้าส์</td>", StringComparison.Ordinal);
        var r2 = html.IndexOf("- ลานจอดรถ", StringComparison.Ordinal);

        // each property's own รวม row follows its rows, with its own total and nothing summed across properties
        var t1 = html.IndexOf("111,000.00", StringComparison.Ordinal);
        var t2 = html.IndexOf("222,000.00", StringComparison.Ordinal);

        new[] { group, h1, r1, t1, h2, r2, t2 }.Should().OnlyContain(i => i >= 0).And.BeInAscendingOrder();
        html.Split("<td>รวม</td>").Length.Should().Be(3); // two รวม rows
        html.Should().NotContain("333,000.00");
    }

    [Fact]
    public async Task A_development_property_with_a_number_prints_it_in_its_cost_header()
    {
        var model = new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Buildings = [DevProperty("บ้านเดี่ยว", "เนื้อที่ภายใน", number: 3)] }] };

        var valuation = await RenderAsync("{{ cost_blds = []; cost_dev = model.all_buildings }}{{ include 'partials/book-building-cost' }}", model);

        valuation.Should().Contain("สิ่งปลูกสร้างรายการที่ 3 : บ้านเดี่ยว");
    }

    private static Task<string> RenderCostSummaryAsync(decimal? overrideValue) =>
        RenderAsync("{{ include 'partials/book-funcs' }}{{ grp = model.property_groups[0]; ref_pass = false; n6 = 0; any6 = false; h6 = '6.'; multi6 = false }}{{ include 'partials/book-flow-methods' }}",
            new AppraisalSummaryModel
            {
                ApproachValues = [new ApproachValueRow { GroupNumber = 1, ApproachType = "Cost", IsReference = false }],
                PropertyGroups = [new PropertyGroupDetail { GroupNumber = 1, Buildings = [CostBuilding(null)] }],
                Groups =
                [
                    new SummaryGroupRow
                    {
                        GroupNumber = 1, LandSubtotal = 5_000_000m, BuildingSubtotal = 19_699_000m, BuildingSubtotalComputed = 19_699_000m,
                        BuildingOverrideValue = overrideValue, GroupTotal = (overrideValue ?? 19_699_000m) + 5_000_000m
                    }
                ]
            });

    private static string CostSummary(string html)
    {
        var start = html.IndexOf("สรุปมูลค่าทรัพย์สินโดยวิธีต้นทุน", StringComparison.Ordinal);
        return html[start..];
    }

    [Fact]
    public async Task The_cost_summary_prints_one_building_row_with_the_value_actually_used()
    {
        var summary = CostSummary(await RenderCostSummaryAsync(2_000_000m));

        summary.Should().Contain("<span>สิ่งปลูกสร้าง จำนวน 1 รายการ</span><span>เป็นเงิน</span><span>2,000,000</span>")
            .And.NotContain("(คำนวณ)").And.NotContain("ปรับโดยผู้ประเมิน").And.NotContain("19,699,000")
            .And.Contain("5,000,000").And.Contain("7,000,000"); // the land line and the total
    }

    [Fact]
    public async Task The_cost_summary_prints_the_rounded_computed_value_when_nothing_was_overridden()
    {
        var summary = CostSummary(await RenderCostSummaryAsync(null));

        summary.Should().Contain("สิ่งปลูกสร้าง จำนวน 1 รายการ (ปัดเศษ)").And.Contain("19,699,000")
            .And.NotContain("ปรับโดยผู้ประเมิน");
    }

    [Fact]
    public async Task Section_5_is_renumbered_5_1_methods_5_2_assumptions_5_3_opinion_without_the_highest_use_block()
    {
        var html = await RenderAsync("{{ include 'partials/book-funcs' }}{{ include 'partials/book-flow-tail' }}", new AppraisalSummaryModel
        {
            PriceMethodNarrative = ["ย่อหน้าแรก", "ย่อหน้าที่สอง"],
            AdditionalAssumptions = "ข้อที่ 1\nข้อที่ 2", AppraiserComment = "ความเห็น"
        });

        var s5 = html[html.IndexOf("<span>5.</span>", StringComparison.Ordinal)..html.IndexOf("<span>6.</span>", StringComparison.Ordinal)];
        new[] { "<span>5.1</span><span>วิธีการประเมินมูลค่า</span>", "<span>5.2</span><span>สมมติฐานเพิ่มเติม / พิเศษ</span>", "<span>5.3</span><span>ความเห็นผู้ประเมิน</span>" }
            .Select(h => s5.IndexOf(h, StringComparison.Ordinal)).Should().BeInAscendingOrder().And.NotContain(-1);
        s5.Should().NotContain("การใช้ประโยชน์สูงสุด").And.NotContain("<span>5.4</span>").And.NotContain("วิธีที่ใช้สรุปมูลค่า")
            .And.Contain("<p class=\"bk-p\">ย่อหน้าแรก</p>").And.Contain("<p class=\"bk-p\">ย่อหน้าที่สอง</p>");
    }

    private static BuildingSection FullBuilding() => new()
    {
        HouseNumber = "10/1", BuildingTypeText = "บ้านเดี่ยว", ModelName = "แบบ A", OwnerName = "นายตัวอย่าง", BuiltOnTitleNumber = "12345", SizeText = "8 x 10", UsableArea = 80m
    };

    private const string SizeRow = ">ขนาดอาคาร (กว้าง x ยาว)<";
    private static readonly string[] FiveRows = [">เลขที่บ้าน<", ">ลักษณะอาคาร<", ">ชื่อแบบ<", ">ผู้ถือกรรมสิทธิ์อาคาร<", ">ปลูกสร้างบนโฉนดเลขที่<"];

    [Fact]
    public async Task In_the_land_and_building_book_the_building_page_starts_at_the_size_row_because_the_summary_table_has_the_five_rows()
    {
        var html = await RenderAsync("{{ include 'partials/book-funcs' }}{{ b = model.all_buildings[0]; bld_k = 1; bld_in_summary_table = true }}{{ include 'partials/book-building-block' }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Buildings = [FullBuilding()] }] });

        html.Should().Contain("สิ่งปลูกสร้างรายการที่ 1").And.Contain("บ้านเดี่ยว").And.Contain(SizeRow).And.Contain(">พื้นที่ใช้สอยรวม<");
        foreach (var row in FiveRows) html.Should().NotContain(row);
        html.Should().NotContain("นายตัวอย่าง");
    }

    [Fact]
    public async Task The_construction_book_keeps_the_five_rows_because_it_has_no_summary_table()
    {
        var html = await RenderAsync("{{ include 'partials/book-funcs' }}{{ b = model.all_buildings[0]; bld_k = 1 }}{{ include 'partials/book-building-block' }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Buildings = [FullBuilding()] }] });

        foreach (var row in FiveRows) html.Should().Contain(row);
        html.Should().Contain("นายตัวอย่าง").And.Contain(SizeRow);
    }

    [Fact]
    public async Task The_standard_book_sets_and_resets_the_flag_around_each_building_page()
    {
        var html = await RenderAsync("{{ include 'partials/book-flow-standard' }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Lands = [new LandSection()], Buildings = [FullBuilding()] }] });

        // the 3.2 table prints the house number; the building block below it does not repeat the five rows
        html.Should().Contain(">10/1<").And.Contain(SizeRow);
        foreach (var row in FiveRows) html.Should().NotContain(row);
    }

    // ── Per-sqm usable area ──────────────────────────────────────────────────────

    [Fact]
    public void Per_sqm_final_value_prints_the_group_usable_area()
    {
        var fv = new PricingGridCommon.FinalValueRow { FinalValue = 50000m, FinalValueOverride = 50000m, IndicatedValue = 6_000_000m };
        var area = PricingGridCommon.SubjectUsableArea(new Dictionary<string, string?> { ["14"] = "1,234.5" });

        area.Should().Be(1234.5m);
        PricingGridCommon.FinalRows(fv, "PerSqm", "มูลค่าสุทธิ", true, area)
            .Single(r => r.Label == "พื้นที่ใช้สอย").Value.Should().Be("1,234.50");
        PricingGridCommon.FinalRows(fv, "PerSqm", "มูลค่าสุทธิ", true)
            .Single(r => r.Label == "พื้นที่ใช้สอย").Value.Should().BeNull();
        PricingGridCommon.SubjectUsableArea(new Dictionary<string, string?> { ["14"] = null }).Should().BeNull();
    }

    // ── Empty cells / empty chapter ──────────────────────────────────────────────

    [Fact]
    public async Task Wqs_price_band_prints_a_dash_for_an_empty_cell()
    {
        var model = new AppraisalSummaryModel { WqsSections = [new WqsSection
        {
            Factors = [new WqsFactorRow { FactorName = "ทำเล", ComparableScores = ["8"], ComparableWeighted = ["8.00"] }],
            ComparableHeaders = [new MarketHeader { Title = "ตลาด 1" }],
            Chunks = [new TableChunk { Start = 0, Count = 1 }],
            PriceRows = [new WqsPriceRow { Label = "ราคาขาย", Values = [null] }]
        }] };

        var html = await RenderAsync("{{ w = model.wqs_sections[0] }}{{ include 'partials/section-wqs' }}", model);

        html.Should().MatchRegex("<td>ราคาขาย</td>\\s*<td colspan=\"5\"></td>\\s*<td class=\"num\" colspan=\"2\">-</td>");
    }

    [Fact]
    public async Task Valuation_chapter_skips_the_no_data_dash_when_the_caller_prints_more()
    {
        const string tpl = "{{ val_no = '4'; val_id = 's4'; val_title = 'การประเมินมูลค่า'; val_has_more = HAS; include 'partials/book-valuation' }}";
        (await RenderAsync(tpl.Replace("HAS", "true"), new AppraisalSummaryModel())).Should().NotContain("<span class=\"na\">-</span>");
        (await RenderAsync(tpl.Replace("HAS", "false"), new AppraisalSummaryModel())).Should().Contain("<span class=\"na\">-</span>");
    }

    [Fact]
    public async Task Period_growth_hides_the_single_rate_and_interval_on_both_lease_pages()
    {
        var periods = new[] { new GrowthPeriodRow { FromYear = 1, ToYear = 5, GrowthRatePercent = 3m } };
        var model = new AppraisalSummaryModel
        {
            LeaseholdSections = [new LeaseholdSection { LandGrowthIsPeriod = true, LandGrowthRatePercent = 0m, LandGrowthIntervalYears = 1, LandGrowthPeriods = periods }],
            ProfitRentSections = [new ProfitRentSection { GrowthIsPeriod = true, GrowthRatePercent = 0m, GrowthIntervalYears = 1, GrowthPeriods = periods }]
        };

        var lh = await RenderAsync("{{ lh = model.leasehold_sections[0] }}{{ include 'partials/section-leasehold' }}", model);
        var pr = await RenderAsync("{{ pr = model.profit_rent_sections[0] }}{{ include 'partials/section-profitrent' }}", model);

        foreach (var html in new[] { lh, pr })
            html.Should().Contain("รายช่วง (ดูตารางช่วงการเติบโต)").And.NotContain("ช่วงเวลาการเติบโต</td>").And.NotContain("0.0000 %");
    }

    [Fact]
    public async Task Frequency_growth_keeps_the_rate_and_interval()
    {
        var model = new AppraisalSummaryModel { ProfitRentSections = [new ProfitRentSection { GrowthRateType = "ตามความถี่ (ทุกช่วงปี)", GrowthRatePercent = 2m, GrowthIntervalYears = 3 }] };
        var html = await RenderAsync("{{ pr = model.profit_rent_sections[0] }}{{ include 'partials/section-profitrent' }}", model);
        html.Should().Contain("ตามความถี่ (ทุกช่วงปี)").And.Contain("2.0000 %").And.Contain("3 ปี").And.NotContain("รายช่วง");
    }

    private static int CountOf(string s, string what) =>
        (s.Length - s.Replace(what, "").Length) / what.Length;
}
