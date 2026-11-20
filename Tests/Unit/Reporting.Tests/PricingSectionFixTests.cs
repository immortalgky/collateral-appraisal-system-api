using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers;
using Reporting.Application.Providers.Sections;
using Reporting.Infrastructure.Rendering;
using HeaderRow = Reporting.Application.Providers.Sections.IncomeSectionLoader.HeaderRow;

namespace Reporting.Tests;

/// <summary>
/// Book pricing pages against the pricing screens (fictional figures): the appraiser's override printed beside the computed
/// figure, machinery as the screen derives it, the HBU = No land split, the leasehold uncovered-land term, and a method of 0
/// treated as Pending.
/// </summary>
public class PricingSectionFixTests
{
    private static Task<string> RenderAsync(string template, AppraisalSummaryModel model) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(template, model, CancellationToken.None);

    // ── 1. Building Cost / Profit Rent overrides ──────────────────────────────────

    [Theory]
    [InlineData(4_000_000d, 4_136_000d, 4_000_000d)]   // typed over the computed total
    [InlineData(4_136_000d, 4_136_000d, null)]        // the computed total itself
    [InlineData(null, 4_136_000d, null)]             // nothing saved
    public void Building_override_is_the_used_value_only_when_it_differs_from_the_computed_total(
        double? used, double computed, double? expected)
        => AppraisalSummaryLandBuildingDataProvider
            .BuildingOverride((decimal?)used, (decimal)computed)
            .Should().Be((decimal?)expected);

    [Fact]
    public async Task Building_cost_prints_the_computed_total_then_the_override_and_never_calls_a_typed_value_rounded()
    {
        const string t = "{{ cost_blds = [model.all_buildings[0]]; cost_dev = []; cost_total = 4000000; cost_computed = 4136000; cost_override = 4000000; include 'partials/book-building-cost' }}";
        var model = new AppraisalSummaryModel
        {
            PropertyGroups = [new PropertyGroupDetail { Buildings = [new BuildingSection { CostRows = [new BuildingCostRow { AreaItemName = "บ้าน" }] }] }]
        };

        var html = await RenderAsync(t, model);

        html.Should().Contain("รวมมูลค่าสิ่งปลูกสร้าง (คำนวณ)").And.Contain("4,136,000.00")
            .And.Contain("ปรับโดยผู้ประเมิน").And.Contain("4,000,000.00")
            .And.NotContain("ตามสภาพปัจจุบัน").And.NotContain("ปัดเศษ");
    }

    [Theory]
    [InlineData(1_325_032_212.02, false, null, 1_325_032_000d, false)] // system figure rounded
    [InlineData(1_325_032_212.02, false, null, 1_320_424_000d, true)]  // typed
    [InlineData(0d, true, 7_260_000d, 7_259_992.74, true)]              // building included, typed 7.26 below
    [InlineData(0d, true, 7_260_000d, 7_260_000d, false)]                // building included, untouched
    [InlineData(1_000_400d, true, 5_000_000d, 6_000_000d, false)]        // PV + building, rounded to the thousand
    public void Profit_rent_value_is_edited_when_it_is_not_the_system_figure_rounded(
        double pv, bool includeBuilding, double? building, double used, bool edited)
    {
        var (withBuilding, isEdited) = ProfitRentSectionLoader.Resolve((decimal)pv, includeBuilding, (decimal?)building, (decimal)used);

        isEdited.Should().Be(edited);
        withBuilding.Should().Be(includeBuilding ? (decimal)pv + (decimal)building! : null);
    }

    [Fact]
    public async Task Profit_rent_labels_a_typed_value_as_the_appraisers_and_only_a_rounded_one_as_rounded()
    {
        const string t = "{{ pr = model.profit_rent_sections[0] }}{{ include 'partials/section-profitrent' }}";
        ProfitRentSection Section(bool edited) => new()
        {
            TotalPresentValue = 1_325_032_212.02m, EffectiveFinalValue = 1_320_424_000m, IsValueEdited = edited,
            IncludeBuildingCost = true, BuildingCostValue = 7_260_000m, ValueIncludingBuilding = 1_332_292_212.02m
        };

        var edited = await RenderAsync(t, new AppraisalSummaryModel { ProfitRentSections = [Section(true)] });
        var rounded = await RenderAsync(t, new AppraisalSummaryModel { ProfitRentSections = [Section(false)] });

        edited.Should().Contain("ปรับโดยผู้ประเมิน").And.NotContain("มูลค่าทรัพย์สิน (ปัดเศษ)")
            .And.Contain("บวก มูลค่าสิ่งปลูกสร้างหลังหักค่าเสื่อมราคา").And.Contain("7,260,000.00")
            .And.Contain("รวมมูลค่าทรัพย์สิน (คำนวณ)").And.Contain("1,332,292,212.02");
        rounded.Should().Contain("มูลค่าทรัพย์สิน (ปัดเศษ)").And.NotContain("ปรับโดยผู้ประเมิน");
    }

    // ── 2. Machinery cost ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("01", true, 2548, 20, 100_000, 0.8, 1, 1, true)]   // ordinary machine
    [InlineData("01", true, 1991, 20, 100_000, 0.8, 1, 1, true)]   // far past its life: R floors at 5, still worth something
    [InlineData("03", true, 2548, 20, 100_000, 0.8, 1, 1, false)]  // not found: locked
    [InlineData("01", false, 2548, 20, 100_000, 0.8, 1, 1, false)] // price certification withdrawn: locked
    [InlineData("01", null, 2548, 20, 100_000, 0.8, 1, 1, true)]   // certification not answered is not a withdrawal
    [InlineData("01", true, 2548, 20, 0, 0.8, 1, 1, false)]        // no RCN
    [InlineData("01", true, 2548, 20, 100_000, 0, 1, 1, false)]    // no condition factor
    public void Machine_market_demand_is_the_screens_FMV_above_zero(
        string condition, bool? certified, int year, double life, double rcn, double c, double f, double e, bool demand)
        => CostMachineSectionLoader
            .ScreenMarketDemand(condition, certified, year, (decimal)life, (decimal)rcn, (decimal)c, (decimal)f, (decimal)e, 2569)
            .Should().Be(demand);

    // ── 3. Income, HBU = No ───────────────────────────────────────────────────────

    [Fact]
    public async Task Income_hbu_no_prints_the_split_land_value_and_the_total_even_when_a_value_was_typed()
    {
        var h = new HeaderRow
        {
            TemplateCode = "dcf-hotel", TemplateName = "DCF - Hotel", TotalNumberOfYears = 2, TotalNumberOfDayInYear = 365,
            DiscountedRate = 9m, IsHighestBestUsed = false, IndicatedValue = 880_000_000m, FinalValueRounded = 0m,
            HighestBestUsed_AreaRai = 1, HighestBestUsed_PricePerSqWa = 1_000_000m,
            Summary_GrossRevenueJson = "[10,10]", Summary_TerminalRevenueJson = "[0]", Summary_TotalNetJson = "[100]",
            Summary_DiscountJson = "[0.9]", Summary_PresentValueJson = "[1000]"
        };
        var s = IncomeSectionLoader.Build(new PickedMethod { GroupNumber = 1 }, h, []);

        s.HbuLandValue.Should().Be(400_000_000m);
        s.HbuTotalValue.Should().Be(400_001_000m);

        var html = await RenderAsync("{{ inc = model.income_sections[0] }}{{ include 'partials/section-income' }}",
            new AppraisalSummaryModel { IncomeSections = [s] });
        html.Should().Contain("บวก มูลค่าที่ดินส่วนที่แยกประเมิน (HBU)").And.Contain("400,000,000.00")
            .And.Contain("รวม (มูลค่าจากรายได้ + ที่ดินส่วนที่แยกประเมิน)").And.Contain("400,001,000.00")
            .And.Contain("ปรับโดยผู้ประเมิน").And.Contain("880,000,000.00")
            .And.NotContain("มูลค่าทรัพย์สิน (ปัดเศษ)");
    }

    [Theory]
    [InlineData(true, 12_000d, null, 0d, 12_000d, false)]            // HBU yes, typed = the final value
    [InlineData(true, 12_000d, null, 0d, 13_000d, true)]             // HBU yes, typed over it
    [InlineData(false, 0d, 5_000d, 400_000_000d, 400_005_000d, false)] // HBU no: floor(5,000 + land)
    [InlineData(false, 0d, null, 400_000_000d, 880_000_000d, true)]  // typed over land alone
    [InlineData(false, 0d, 2_500d, 0d, 3_000d, false)]               // no land: adjusted value rounded
    public void Income_value_is_edited_when_the_typed_value_is_not_the_system_figure_rounded(
        bool hbu, double final, double? adjust, double land, double indicated, bool edited)
        => IncomeSectionLoader.IsEdited(hbu, (decimal)final, (decimal?)adjust, (decimal)land, (decimal)indicated).Should().Be(edited);

    [Fact]
    public void Income_value_is_never_edited_without_a_typed_value()
        => IncomeSectionLoader.IsEdited(false, 0m, null, 400_000_000m, null).Should().BeFalse();

    // ── 4. Leasehold, partial land usage ──────────────────────────────────────────

    [Fact]
    public void Leasehold_uncovered_land_makes_the_printed_figures_add_up()
    {
        // PV estimate 10,000,000; 300 sq.wa in all, 100 under the lease, 50,000 per sq.wa -> 200 x 50,000 not covered.
        var uncovered = LeaseholdSectionLoader.Uncovered(true, 20_000_000m, 10_000_000m, 50_000m);

        uncovered.Should().NotBeNull();
        uncovered!.Value.Price.Should().Be(10_000_000m);
        uncovered.Value.Area.Should().Be(200m);
        (10_000_000m + uncovered.Value.Price).Should().Be(20_000_000m);
    }

    [Theory]
    [InlineData(false, 20_000_000d, 50_000d)]   // not a partial usage
    [InlineData(true, null, 50_000d)]           // no stored total
    [InlineData(true, 20_000_000d, 0d)]          // no price
    [InlineData(true, 20_000_000d, null)]
    public void Leasehold_uncovered_land_is_absent_without_a_partial_usage_total_and_price(bool partial, double? net, double? price)
        => LeaseholdSectionLoader.Uncovered(partial, (decimal?)net, 10_000_000m, (decimal?)price).Should().BeNull();

    [Fact]
    public async Task Leasehold_partial_usage_prints_estimate_plus_uncovered_land_equals_total()
    {
        var s = new LeaseholdSection
        {
            IsPartialUsage = true, FinalValueRounded = 10_000_000m, EstimateNetPrice = 20_000_000m, PricePerSqWa = 50_000m,
            PartialLandArea = 100m, PartialLandPrice = 5_000_000m, UncoveredLandArea = 200m, UncoveredLandPrice = 10_000_000m
        };

        var html = await RenderAsync("{{ lh = model.leasehold_sections[0] }}{{ include 'partials/section-leasehold' }}",
            new AppraisalSummaryModel { LeaseholdSections = [s] });

        html.Should().Contain("ประมาณการมูลค่าสิทธิการเช่า").And.Contain("10,000,000.00")
            .And.Contain("บวก ที่ดินส่วนที่ไม่ได้อยู่ในสัญญาเช่า").And.Contain("200.00 ตร.วา × 50,000.00")
            .And.Contain("รวม มูลค่าสิทธิการเช่าและที่ดินส่วนที่ไม่อยู่ในสัญญาเช่า").And.Contain("20,000,000.00");
    }

    // ── 5. A method of 0 is Pending ───────────────────────────────────────────────

    private static PricingMethodPicker.Row Method(string type, decimal? value, bool selected, bool approachSelected, Guid approach) => new()
    {
        PropertyGroupId = Guid.Empty, GroupNumber = 1, ApproachId = approach, ApproachType = "Income", ApproachSelected = approachSelected,
        PricingMethodId = Guid.NewGuid(), MethodType = type, MethodValue = value, MethodSelected = selected
    };

    [Fact]
    public void A_reference_method_of_zero_is_not_printed_but_one_with_a_value_is()
    {
        var a = Guid.NewGuid();
        var rows = new[] { Method("Leasehold", 0m, false, false, a), Method("ProfitRent", 7_000m, false, false, a), Method("Income", null, false, false, a) };

        var picked = PricingMethodPicker.Pick(rows, ["Leasehold", "ProfitRent", "Income"]);

        picked.Select(p => p.MethodType).Should().Equal("ProfitRent");
    }

    [Fact]
    public void With_nothing_selected_only_methods_carrying_a_value_stand_in_and_a_zero_does_not()
    {
        var a = Guid.NewGuid();
        var rows = new[] { Method("Leasehold", 0m, false, true, a), Method("ProfitRent", 7_000m, false, true, a) };

        PricingMethodPicker.Pick(rows, ["Leasehold", "ProfitRent"]).Select(p => p.MethodType).Should().Equal("ProfitRent");
    }

    [Fact]
    public void A_selected_method_is_printed_even_when_its_value_is_zero()
    {
        var a = Guid.NewGuid();

        PricingMethodPicker.Pick([Method("Leasehold", 0m, true, true, a)], ["Leasehold"]).Should().ContainSingle();
    }

    // ── Weighted net value (Sale Adjustment Grid) ─────────────────────────────────

    [Fact]
    public void Weighted_net_value_is_the_screens_sum_of_each_column_at_two_decimals()
    {
        // Fictional figures shaped like a reloaded 3-comparable grid: the weights are stored to 5 dp (.33333), so the
        // screen's per-column products are 24,499.76 + 31,499.69 + 58,239.42, while the save stored 24,500 + 31,500 + 58,240.
        var net = PricingGridCommon.WeightedNetValue(
            [(73_500m, 0.33333m), (94_500m, 0.33333m), (174_720m, 0.33333m)]);

        net.Should().Be(114_238.87m);
        PricingGridCommon.WeightedNetValue([(null, 0.5m), (100m, null)]).Should().BeNull();
    }

    [Fact]
    public void Final_rows_print_the_given_net_value_and_keep_the_other_rows_as_stored()
    {
        var fv = new PricingGridCommon.FinalValueRow { FinalValue = 114_240m, FinalValueOverride = 114_240m, LandArea = 900m, IndicatedValue = 102_816_000m };

        var rows = PricingGridCommon.FinalRows(fv, "PerSqWa", "มูลค่าสุทธิ (ถ่วงน้ำหนัก)", perUnitSuffix: true, netValue: 114_238.87m);

        rows[0].Label.Should().Be("มูลค่าสุทธิ (ถ่วงน้ำหนัก) ต่อตร.วา");
        rows[0].Value.Should().Be("114,238.87");
        rows.Single(r => r.Label == "ราคาต่อตร.วาที่ใช้").Value.Should().Be("114,240.00"); // the rounded / used row is untouched
        PricingGridCommon.FinalRows(fv, "PerSqWa", "x", true)[0].Value.Should().Be("114,240.00"); // no net given: stored figure
    }
}
