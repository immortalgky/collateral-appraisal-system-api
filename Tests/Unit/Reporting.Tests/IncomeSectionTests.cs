using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers.Sections;
using Reporting.Infrastructure.Rendering;
using HeaderRow = Reporting.Application.Providers.Sections.IncomeSectionLoader.HeaderRow;
using TreeRow = Reporting.Application.Providers.Sections.IncomeSectionLoader.TreeRow;

namespace Reporting.Tests;

/// <summary>Income approach pages: line-item cash-flow table, 6-year blocks, Direct Capitalization, assumption summaries.</summary>
public class IncomeSectionTests
{
    private static readonly Guid SecIncome = Guid.NewGuid(), SecExp = Guid.NewGuid();
    private static readonly Guid CatOp = Guid.NewGuid(), CatDirect = Guid.NewGuid(), CatGop = Guid.NewGuid(), CatFixed = Guid.NewGuid();
    private static readonly Guid AsmRoom = Guid.NewGuid(), AsmUtil = Guid.NewGuid(), AsmRoomCost = Guid.NewGuid(), AsmFee = Guid.NewGuid();

    private static string Arr(int n, decimal v) => "[" + string.Join(",", Enumerable.Repeat(v, n)) + "]";
    private static string Seq(int n, Func<int, decimal> f) => "[" + string.Join(",", Enumerable.Range(0, n).Select(f)) + "]";

    /// <summary>Hotel-shaped tree: Income(Operating Income: room 01, utility 13) / Expenses(Direct: room cost 13, GOP, Fixed: fee 13 of GOP).</summary>
    private static List<TreeRow> HotelTree(int n) =>
    [
        new() { SectionId = SecIncome, SectionType = "income", SectionName = "Income", TotalSectionValuesJson = Arr(n, 1100),
                CategoryId = CatOp, CategoryType = "income", CategoryName = "Operating Income", TotalCategoryValuesJson = Arr(n, 1100),
                AssumptionId = AsmRoom, AssumptionName = "Room Rental Income", TotalAssumptionValuesJson = Arr(n, 1000), MethodTypeCode = "01",
                MethodDetailJson = """{"sumSaleableArea":120,"avgRoomRate":1800,"occupancyRateFirstYearPct":70,"occupancyRatePct":5,"occupancyRateYrs":1,"increaseRatePct":10,"increaseRateYrs":3}""" },
        new() { SectionId = SecIncome, SectionType = "income", SectionName = "Income", TotalSectionValuesJson = Arr(n, 1100),
                CategoryId = CatOp, CategoryType = "income", CategoryName = "Operating Income", TotalCategoryValuesJson = Arr(n, 1100),
                AssumptionId = AsmUtil, AssumptionName = "Utility Income", TotalAssumptionValuesJson = Arr(n, 100), MethodTypeCode = "13",
                MethodDetailJson = $$$"""{"proportionPct":10,"refTarget":{"kind":"assumption","dbId":"{{{AsmRoom}}}"}}""" },
        new() { SectionId = SecExp, SectionType = "expenses", SectionName = "Expenses / Costs", TotalSectionValuesJson = Arr(n, 300),
                CategoryId = CatDirect, CategoryType = "expenses", CategoryName = "Direct Operating Expenses", TotalCategoryValuesJson = Arr(n, 150),
                AssumptionId = AsmRoomCost, AssumptionName = "Room Cost", TotalAssumptionValuesJson = Arr(n, 150), MethodTypeCode = "13",
                MethodDetailJson = $$$"""{"proportionPct":15,"refTarget":{"kind":"assumption","clientId":"assumption:{{{AsmRoom}}}"}}""" },
        new() { SectionId = SecExp, SectionType = "expenses", SectionName = "Expenses / Costs", TotalSectionValuesJson = Arr(n, 300),
                CategoryId = CatGop, CategoryType = "gop", CategoryName = "Gross Operating Profit (GOP)", TotalCategoryValuesJson = Arr(n, 950) },
        new() { SectionId = SecExp, SectionType = "expenses", SectionName = "Expenses / Costs", TotalSectionValuesJson = Arr(n, 300),
                CategoryId = CatFixed, CategoryType = "fixedExps", CategoryName = "Fixed Charge", TotalCategoryValuesJson = Arr(n, 150),
                AssumptionId = AsmFee, AssumptionName = "Administration Fee", TotalAssumptionValuesJson = Arr(n, 150), MethodTypeCode = "13",
                MethodDetailJson = $$$"""{"proportionPct":5,"refTarget":{"kind":"category","dbId":"{{{CatGop}}}"},"startIn":2}""" },
    ];

    /// <summary>Summary arrays shaped like IncomeCalculationService: NOI has N entries, DCF arrays N-1 with the terminal value at N-2.</summary>
    private static HeaderRow DcfHeader(int n) => new()
    {
        TemplateCode = "dcf-hotel", TemplateName = "DCF - Hotel", TotalNumberOfYears = n, TotalNumberOfDayInYear = 365,
        CapitalizeRate = 7m, DiscountedRate = 9m, FinalValueRounded = 12_000m, IsHighestBestUsed = true,
        Summary_ContractRentalFeeJson = Arr(n, 0),
        Summary_GrossRevenueJson = Seq(n, i => 800 + i),
        Summary_GrossRevenueProportionalJson = Arr(n, 72.73m),
        Summary_TerminalRevenueJson = Seq(n - 1, i => i == n - 2 ? 11_500 : 0),
        Summary_TotalNetJson = Seq(n - 1, i => i == n - 2 ? 12_300 : 800 + i),
        Summary_DiscountJson = Seq(n - 1, i => 0.9m - i * 0.05m),
        Summary_PresentValueJson = Seq(n - 1, i => 100 + i),
    };

    private static IncomeSection Build(HeaderRow h, List<TreeRow> tree) =>
        IncomeSectionLoader.Build(new PickedMethod { GroupNumber = 1, Context = new PricingMethodContext { Remark = "อัตราเข้าพักเฉลี่ยในพื้นที่" } }, h, tree);

    private static Task<string> RenderAsync(IncomeSection s) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(
            "{{ inc = model.income_sections[0] }}{{ include 'partials/section-income' }}",
            new AppraisalSummaryModel { IncomeSections = [s] }, CancellationToken.None);

    private static IncomeCashFlowRow Row(IncomeCashFlowBlock b, string label) => b.Rows.Single(r => r.Label == label);

    [Fact]
    public async Task Hotel_dcf_six_years_prints_every_line_then_noi_then_dcf_rows_with_year_six_dashed()
    {
        var s = Build(DcfHeader(6), HotelTree(6));

        s.IsDirectCapitalization.Should().BeFalse();
        var b = s.CashFlowBlocks.Should().ContainSingle().Subject;
        b.Years.Should().Equal(1, 2, 3, 4, 5, 6);
        b.Rows.Select(r => r.Label).Should().ContainInOrder(
            "รายได้ (Income)", "Operating Income", "Room Rental Income", "Utility Income", "รวมรายได้",
            "ค่าใช้จ่าย (Expenses)", "Direct Operating Expenses", "Room Cost", "รวม Direct Operating Expenses",
            "กำไรขั้นต้นจากการดำเนินงาน (GOP)", "Fixed Charge", "Administration Fee", "รวม Fixed Charge",
            "รายได้สุทธิ (NOI)", "NOI/รายได้รวม (%)", "คิดลดกระแสเงินสด (DCF)",
            "มูลค่าซากสุดท้าย (Terminal Value)", "กระแสเงินสดสุทธิ", "ตัวคูณลด @ 9.00 %", "มูลค่าปัจจุบัน");
        // A single income category: no "รวม Operating Income" next to "รวมรายได้".
        b.Rows.Should().NotContain(r => r.Label == "รวม Operating Income");
        // Contract rent is all zero for a plain hotel → no row.
        b.Rows.Should().NotContain(r => r.Label.Contains("ค่าเช่าตามสัญญา"));
        Row(b, "กำไรขั้นต้นจากการดำเนินงาน (GOP)").Cells.Should().AllBeEquivalentTo(950m);
        Row(b, "รายได้สุทธิ (NOI)").Cells.Should().Equal(800m, 801m, 802m, 803m, 804m, 805m);
        Row(b, "มูลค่าซากสุดท้าย (Terminal Value)").Cells.Should().Equal(null, null, null, null, 11_500m, null);
        Row(b, "กระแสเงินสดสุทธิ").Cells.Should().Equal(800m, 801m, 802m, 803m, 12_300m, null);
        Row(b, "ตัวคูณลด @ 9.00 %").Format.Should().Be("N6");
        Row(b, "มูลค่าปัจจุบัน").Cells[5].Should().BeNull();
        s.TerminalBaseNoi.Should().Be(805m);
        s.TerminalValue.Should().Be(11_500m);
        s.PresentValueTotal.Should().Be(510m);

        var html = await RenderAsync(s);
        html.Should().Contain("Income Approach – Discounted Cash Flow")
            .And.Contain("DCF - Hotel").And.Contain("365 วัน").And.Contain("อัตราคิดลด (Discount Rate)").And.Contain("9.00 %")
            .And.Contain("<th>ปีที่ 6</th>")
            .And.Contain("มูลค่าซากสุดท้าย = รายได้สุทธิปีที่ 6 805.00 ÷ Cap Rate 7.00 % = <b>11,500.00</b> บาท (คิดลดที่ปีที่ 5)")
            .And.Contain("รวมมูลค่าปัจจุบัน").And.Contain("510.00 บาท")
            .And.Contain("มูลค่าทรัพย์สิน (ปัดเศษ)").And.Contain("12,000.00")
            .And.Contain("หมายเหตุและสมมติฐาน :").And.Contain("อัตราเข้าพักเฉลี่ยในพื้นที่");
        html.Should().NotContain("มูลค่าหลังปรับโดยผู้ประเมิน");
    }

    [Fact]
    public async Task More_than_six_years_splits_into_blocks_of_six_repeating_the_label_column()
    {
        var s = Build(DcfHeader(10), HotelTree(10));

        s.CashFlowBlocks.Should().HaveCount(2);
        s.CashFlowBlocks[0].Years.Should().Equal(1, 2, 3, 4, 5, 6);
        s.CashFlowBlocks[1].Years.Should().Equal(7, 8, 9, 10);
        s.CashFlowBlocks[1].Rows.Select(r => r.Label).Should().Equal(s.CashFlowBlocks[0].Rows.Select(r => r.Label));
        Row(s.CashFlowBlocks[1], "มูลค่าซากสุดท้าย (Terminal Value)").Cells.Should().Equal(null, null, 11_500m, null);
        Row(s.CashFlowBlocks[0], "มูลค่าซากสุดท้าย (Terminal Value)").Cells.Should().OnlyContain(c => c == null);

        var html = await RenderAsync(s);
        html.Split("รายการ (บาท)").Length.Should().Be(3);
        html.Should().Contain("<th>ปีที่ 10</th>").And.Contain("(คิดลดที่ปีที่ 9)");
        // Block 2 (years 7-10) is padded to the 6-year grid so its columns line up with block 1.
        html.Should().Contain("inc-fixed");
        html.Split("<th class=\"inc-pad\"></th>").Length.Should().Be(3);
    }

    [Fact]
    public async Task Direct_capitalization_is_one_year_without_discount_rate_and_prints_noi_over_cap_rate()
    {
        var h = new HeaderRow
        {
            TemplateCode = "direct-apartment", TemplateName = "Direct - Apartment", TotalNumberOfYears = 1, TotalNumberOfDayInYear = 365,
            CapitalizeRate = 6.5m, DiscountedRate = 0m, FinalValueRounded = 6_300_000m, IsHighestBestUsed = true,
            Summary_GrossRevenueJson = "[409500]", Summary_TerminalRevenueJson = "[0]", Summary_TotalNetJson = "[409500]",
            Summary_DiscountJson = "[1]", Summary_PresentValueJson = "[6300000]",
        };
        var s = Build(h, HotelTree(1));

        s.IsDirectCapitalization.Should().BeTrue();
        s.DiscountedRate.Should().BeNull();
        var b = s.CashFlowBlocks.Should().ContainSingle().Subject;
        b.Years.Should().Equal(1);
        b.Rows.Should().NotContain(r => r.Label.StartsWith("ตัวคูณลด") || r.Label.StartsWith("มูลค่าซากสุดท้าย"));
        s.DirectNoi.Should().Be(409_500m);

        var html = await RenderAsync(s);
        html.Should().Contain("Income Approach – Direct Capitalization").And.Contain("Direct - Apartment")
            .And.Contain("ประมาณการรายได้และค่าใช้จ่ายปีแรก")
            .And.Contain("มูลค่าทรัพย์สิน = รายได้สุทธิ 409,500.00 ÷ Cap Rate 6.50 % = <b>6,300,000.00</b> บาท")
            .And.Contain("มูลค่าจากรายได้ (NOI ÷ Cap Rate)");
        html.Should().NotContain("อัตราคิดลด").And.NotContain("0.00 %").And.NotContain("<th>ปีที่ 2</th>").And.NotContain("inc-pad\"></");
    }

    [Fact]
    public void Method_13_resolves_its_reference_to_the_target_name_by_db_id_or_client_id()
    {
        var a = Build(DcfHeader(6), HotelTree(6)).Assumptions;

        a.Select(x => x.Name).Should().Equal("Room Rental Income", "Utility Income", "Room Cost", "Administration Fee");
        a[1].Method.Should().Be("13 สัดส่วน");
        a[1].Summary.Should().Be("10% ของ Room Rental Income");                       // dbId → assumption
        a[2].Summary.Should().Be("15% ของ Room Rental Income");                       // clientId "assumption:<id>"
        a[3].Summary.Should().Be("5% ของ Gross Operating Profit (GOP) · เริ่มปี 2");   // dbId → category

        IncomeSectionLoader.Summarize("13", """{"proportionPct":10,"refTarget":{"kind":"assumption","dbId":"00000000-0000-0000-0000-000000000001"}}""",
            new Dictionary<string, string>()).Should().Be("10% ของ (ไม่พบรายการอ้างอิง)");
    }

    [Fact]
    public void Assumption_summary_reads_the_method_inputs_and_drops_zero_parts()
    {
        var a = Build(DcfHeader(6), HotelTree(6)).Assumptions;
        a[0].Method.Should().Be("01 รายได้ห้องต่อวัน");
        a[0].Summary.Should().Be("120 ห้อง · ADR 1,800 · เข้าพัก 70% +5/ปี · +10% ทุก 3 ปี");

        var none = new Dictionary<string, string>();
        IncomeSectionLoader.Summarize("01", """{"sumSaleableArea":0,"avgRoomRate":1800,"occupancyRateFirstYearPct":0}""", none).Should().Be("ADR 1,800");
        IncomeSectionLoader.Summarize("14", """{"firstYearAmt":3000000,"increaseRatePct":0}""", none).Should().Be("3,000,000 บ./ปี");
        IncomeSectionLoader.Summarize("12", """{"proportionPct":0.1,"newReplacementCost":85000000,"increaseRatePct":2,"increaseRateYrs":1}""", none)
            .Should().Be("0.1% ของต้นทุนทดแทนใหม่ 85,000,000 บาท · +2%/ปี");
        IncomeSectionLoader.Summarize("09", """{"jobPositionDetails":[{"numberOfEmployees":3},{"numberOfEmployees":2}],"sumTotalSalaryPerYear":900000}""", none)
            .Should().Be("5 คน · 900,000 บ./ปี");
        IncomeSectionLoader.Summarize("08", """{"increaseRate":[0,0]}""", none).Should().BeNull();
        IncomeSectionLoader.Summarize("14", "not json", none).Should().BeNull();
    }

    [Fact]
    public async Task Missing_arrays_and_inputs_print_a_plain_dash_never_a_made_up_number()
    {
        var tree = HotelTree(3);
        tree[0] = new TreeRow
        {
            SectionId = SecIncome, SectionType = "income", SectionName = "Income", TotalSectionValuesJson = "[]",
            CategoryId = CatOp, CategoryType = "income", CategoryName = "Operating Income", TotalCategoryValuesJson = "[]",
            AssumptionId = AsmRoom, AssumptionName = "Room Rental Income", TotalAssumptionValuesJson = "[]", MethodTypeCode = "14", MethodDetailJson = "{}",
        };
        var h = new HeaderRow { TemplateCode = "dcf-hotel", TotalNumberOfYears = 3, IsHighestBestUsed = true };
        var s = Build(h, tree);

        Row(s.CashFlowBlocks[0], "Room Rental Income").Cells.Should().OnlyContain(c => c == null).And.HaveCount(3);
        Row(s.CashFlowBlocks[0], "รายได้สุทธิ (NOI)").Cells.Should().OnlyContain(c => c == null);
        s.Assumptions[0].Summary.Should().BeNull();
        s.TerminalValue.Should().BeNull();
        s.PresentValueTotal.Should().BeNull();

        var html = await RenderAsync(s);
        html.Should().Contain("<td class=\"lbl\">Room Rental Income</td><td class=\"ctr\">-</td>")
            .And.Contain("<td>14 จำนวนเงินพร้อมอัตราการเติบโต</td><td>-</td>");
        html.Should().NotContain("มูลค่าซากสุดท้าย =").And.NotContain("รวมมูลค่าปัจจุบัน");
    }

    [Fact]
    public async Task Appraiser_adjustment_hbu_land_and_indicated_value_print_when_present()
    {
        var h = DcfHeader(6);
        h = new HeaderRow
        {
            TemplateCode = h.TemplateCode, TotalNumberOfYears = 6, CapitalizeRate = 7m, DiscountedRate = 9m,
            Summary_GrossRevenueJson = h.Summary_GrossRevenueJson, Summary_PresentValueJson = h.Summary_PresentValueJson,
            FinalValueRounded = 12_000m, FinalValueAdjust = 11_000m, IndicatedValue = 15_000m,
            IsHighestBestUsed = false, HighestBestUsed_AreaWa = 10m, HighestBestUsed_PricePerSqWa = 100m,
        };
        var s = Build(h, HotelTree(6));

        s.HbuLandValue.Should().Be(1_000m);
        s.HbuAreaText.Should().Be("0 - 0 - 10 ไร่ หรือ 10 ตารางวา");
        s.EffectiveFinalValue.Should().Be(15_000m);

        var html = await RenderAsync(s);
        html.Should().Contain("มูลค่าหลังปรับโดยผู้ประเมิน").And.Contain("11,000.00")
            .And.Contain("มูลค่าที่ดินส่วนที่แยกประเมิน (HBU)").And.Contain("1,000.00")
            .And.Contain("ปรับโดยผู้ประเมิน").And.Contain("15,000.00").And.NotContain("มูลค่าที่ผู้ประเมินกำหนด")
            .And.Contain("การปรับมูลค่าสูงสุดและดีที่สุด (Highest and Best Use)")
            .And.Contain("0 - 0 - 10 ไร่ หรือ 10 ตารางวา");

        var raiOnly = IncomeSectionLoader.Build(new PickedMethod(), new HeaderRow { TotalNumberOfYears = 6, IsHighestBestUsed = false, HighestBestUsed_AreaRai = 1 }, HotelTree(6));
        raiOnly.HbuAreaText.Should().Be("1 - 0 - 0 ไร่ หรือ 400 ตารางวา");
        IncomeSectionLoader.Build(new PickedMethod(), new HeaderRow { TotalNumberOfYears = 6, IsHighestBestUsed = false }, HotelTree(6)).HbuAreaText.Should().BeNull();
    }

    [Fact]
    public async Task Room_rate_sub_analysis_prints_under_its_heading_before_the_remark()
    {
        var sub = new PricingSubAnalysis { Heading = "ที่มาของค่าห้องพัก", Caption = "ประเภทห้อง Deluxe", MethodLabel = "WQS", Wqs = new WqsSection() };
        var s = IncomeSectionLoader.Build(new PickedMethod { Context = new PricingMethodContext { Remark = "หมายเหตุทดสอบ" } }, DcfHeader(6), HotelTree(6), [sub]);

        var html = await RenderAsync(s);
        html.Should().Contain("ที่มาของค่าห้องพัก").And.Contain("ประเภทห้อง Deluxe · WQS");
        html.IndexOf("ที่มาของค่าห้องพัก", StringComparison.Ordinal).Should().BeLessThan(html.IndexOf("หมายเหตุทดสอบ", StringComparison.Ordinal));
    }
}
