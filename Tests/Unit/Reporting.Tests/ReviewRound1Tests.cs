using System.Data;
using FluentAssertions;
using Reporting.Application.Formatting;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers;
using Reporting.Application.Providers.Sections;
using Reporting.Tests.Infrastructure;

namespace Reporting.Tests;

/// <summary>Review round 1: the pricing-section and loader fixes that can be checked without a database.</summary>
public class ReviewRound1Tests
{
    // ── 6. "แก้เอง" compares against the figure the net row prints ───────────────────────────────

    [Fact]
    public void The_edited_note_compares_the_rate_used_with_the_printed_net_value_not_the_stored_final_value()
    {
        // stored FinalValue 1,000.00, recomputed net 1,002.00, the appraiser's rate equals the recomputed (printed) net
        var fv = new PricingGridCommon.FinalValueRow { FinalValue = 1_000m, FinalValueOverride = 1_002m, IndicatedValue = 5_000m };

        var rows = PricingGridCommon.FinalRows(fv, "PerSqWa", "มูลค่าสุทธิ", true, netValue: 1_002m);

        rows.Single(r => r.Label.StartsWith("ราคาต่อ")).Note.Should().BeNull();
    }

    [Fact]
    public void The_edited_note_still_shows_when_the_rate_really_differs_from_the_printed_net_value()
    {
        var fv = new PricingGridCommon.FinalValueRow { FinalValue = 1_000m, FinalValueOverride = 1_050m, IndicatedValue = 5_000m };

        var rows = PricingGridCommon.FinalRows(fv, "PerSqWa", "มูลค่าสุทธิ", true, netValue: 1_002m);

        rows.Single(r => r.Label.StartsWith("ราคาต่อ")).Note.Should().Contain("แก้เอง").And.Contain("+48.00");
    }

    // ── 7. Hypothesis "แก้เอง" needs used != computed ─────────────────────────────────────────

    [Fact]
    public void The_hypothesis_total_is_not_flagged_when_the_indicated_value_equals_the_computed_one()
    {
        HypothesisSectionLoader.Final(2_000_000m, 2_000_000m).Rate.Should().BeNull();
        HypothesisSectionLoader.Final(null, 2_000_000m).Rate.Should().BeNull();
        HypothesisSectionLoader.Final(2_100_000m, 2_000_000m).Rate.Should().Contain("แก้เอง").And.Contain("100,000.00");
    }

    // ── 10. One date format for the whole book ─────────────────────────────────────────────────

    [Fact]
    public void Dates_print_as_day_thai_month_buddhist_year_in_the_comparison_header_whatever_the_culture()
    {
        var old = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("th-TH");
            ThaiDateFormatter.Long(new DateTime(2024, 10, 28)).Should().Be("28 ตุลาคม 2567");
            PricingGridCommon.MarketSub(5_000_000m, new DateTime(2024, 3, 5), null, "PerSqWa", null).Should().Be("ซื้อขายจริง · 5 มีนาคม 2567 · /ตร.ว.");
            PricingGridCommon.MarketSub(null, null, null, null, null).Should().Be("ประกาศขาย · - · เหมา");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = old; }
    }

    // ── 12. condo figures come per group, from the groups' property types and their own units ────

    private static AppraisalSummaryModel CondoModel(Dictionary<int, IReadOnlyList<string>> types) => new()
    {
        GroupPropertyTypes = types,
        CondoSection = new CondoSection
        {
            CondoName = "ทั้งหมด", UsableArea = 100m,
            GroupFigures = new Dictionary<int, CondoGroupFigures>
            {
                [1] = new() { CondoName = "ลุมพินี", UsableArea = 35m, BuildingCondition = "ดี", Facilities = "สระว่ายน้ำ" },
                [2] = new() { CondoName = "ไอดีโอ", UsableArea = 65m, BuildingCondition = "พอใช้" }
            }
        }
    };

    [Fact]
    public void Each_condo_only_group_reads_its_own_units_and_never_the_appraisal_wide_totals()
    {
        var model = CondoModel(new() { [1] = ["U"], [2] = ["LSU"] });

        var g1 = ComparisonSubject.Values(model, 1);
        var g2 = ComparisonSubject.Values(model, 2);

        g1["18"].Should().Be("ลุมพินี"); g1["14"].Should().Be("35"); g1["11"].Should().Be("ดี"); g1["32"].Should().Be("สระว่ายน้ำ"); g1["01"].Should().Be("ห้องชุด");
        g2["18"].Should().Be("ไอดีโอ"); g2["14"].Should().Be("65"); g2["11"].Should().Be("พอใช้");
        PricingGridCommon.SubjectUsableArea(g2).Should().Be(65m);
    }

    [Fact]
    public void A_mixed_machine_only_untyped_or_property_less_group_is_not_condo_only()
    {
        var model = CondoModel(new()
        {
            [1] = ["U", "L"],   // a unit next to land
            [2] = ["MAC"],      // machinery only
            [3] = ["U", ""],    // a unit and a property with no type: the untyped one is NOT a unit
            [4] = [""]
        });

        foreach (var g in new[] { 1, 2, 3, 4, 5 })
            ComparisonSubject.Values(model, g).GetValueOrDefault("18").Should().BeNull();
        ComparisonSubject.IsCondoOnly(model, 3).Should().BeFalse();
        ComparisonSubject.IsCondoOnly(model, 5).Should().BeFalse(); // not in the map at all
    }

    // ── 13 + 17. Land loader: ระวาง with the แผ่นที่ fallback, and the "other" transport text ──────

    private static async Task<LandSection> LoadLandAsync(string? rawang, string? mapSheet, string? transport = null, string? transportOther = null, string? titleType = null)
    {
        DataTable T(params (string, Type)[] cols) { var t = new DataTable(); foreach (var (n, ty) in cols) t.Columns.Add(n, ty); return t; }
        var id = Guid.NewGuid();
        var land = T(("LandDetailId", typeof(Guid)), ("TransportationAccessType", typeof(string)), ("TransportationAccessTypeOther", typeof(string)));
        land.Rows.Add(id, (object?)transport ?? DBNull.Value, (object?)transportOther ?? DBNull.Value);
        var titles = T(("LandDetailId", typeof(Guid)), ("TitleNumber", typeof(string)), ("TitleType", typeof(string)), ("Rawang", typeof(string)), ("MapSheetNumber", typeof(string)));
        titles.Rows.Add(id, "12345", (object?)titleType ?? DBNull.Value, (object?)rawang ?? DBNull.Value, (object?)mapSheet ?? DBNull.Value);
        var prms = T(("Group", typeof(string)), ("Code", typeof(string)), ("Description", typeof(string)));
        prms.Rows.Add("Transportation", "01", "รถยนต์");
        prms.Rows.Add("Transportation", "99", "อื่นๆ");
        var sets = new DataSet();
        sets.Tables.Add(land); sets.Tables.Add(titles); sets.Tables.Add(prms);
        sets.Tables.Add(T(("LandDetailId", typeof(Guid))));
        var factory = new FakeSqlConnectionFactory();
        factory.SetMultiResult(sets);
        return (await LandSectionLoader.LoadAllAsync(factory.GetOpenConnection(), Guid.NewGuid())).Single();
    }

    [Theory]
    [InlineData("ระวาง 4", "แผ่น 9", "ระวาง 4")]   // a โฉนด: the ระวาง
    [InlineData(null, "แผ่น 9", "แผ่น 9")]          // an นส.3ก: only the แผ่นที่
    [InlineData("  ", "แผ่น 9", "แผ่น 9")]
    [InlineData(null, null, null)]
    public async Task The_title_sheet_is_the_rawang_and_falls_back_to_the_map_sheet_number_when_blank(string? rawang, string? sheet, string? expected)
    {
        var land = await LoadLandAsync(rawang, sheet);

        land.Titles.Single().MapSheet.Should().Be(expected);
    }

    [Fact]
    public async Task The_other_transport_text_replaces_the_other_label()
    {
        var land = await LoadLandAsync(null, null, "[\"01\",\"99\"]", "เรือโดยสาร");

        land.TransportationAccessType.Should().Be("รถยนต์, เรือโดยสาร");
    }

    // ── Round 2 ────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("DEED", "ระวาง 4", "แผ่น 9", "ระวาง 4")]
    [InlineData("NS3K", "ระวาง เก่า", "แผ่น 9", "แผ่น 9")]   // a stale Rawang on an น.ส.3 ก. does not win
    [InlineData("NS3K", "ระวาง เก่า", null, "ระวาง เก่า")]  // ...but fills in when there is no แผ่นที่
    [InlineData("NS3", "ระวาง 4", "แผ่น 9", "ระวาง 4")]
    [InlineData(null, null, "แผ่น 9", "แผ่น 9")]
    public async Task The_title_sheet_follows_the_document_type(string? type, string? rawang, string? sheet, string? expected)
    {
        var land = await LoadLandAsync(rawang, sheet, titleType: type);

        land.Titles.Single().MapSheet.Should().Be(expected);
    }

    [Fact]
    public void Other_text_prints_only_while_code_99_is_in_the_list()
    {
        var map = new Dictionary<string, string?> { ["01"] = "รถยนต์", ["99"] = "อื่นๆ" };

        LandSectionLoader.JsonCodesToThai("[\"01\",\"99\"]", map, "เรือโดยสาร").Should().Be("รถยนต์, เรือโดยสาร");
        LandSectionLoader.JsonCodesToThai("[\"01\"]", map, "เรือโดยสาร").Should().Be("รถยนต์"); // left over after 99 was unticked
        LandSectionLoader.JsonCodesToThai("01", map, "เรือโดยสาร").Should().Be("รถยนต์");
    }

    private static readonly Func<DateTime, DateTime> Identity = d => d;

    [Fact]
    public void A_date_factor_without_an_offset_is_the_calendar_date_as_written_whatever_the_server_zone()
    {
        PricingGridCommon.DateFactorText("2024-03-05", Identity).Should().Be("5 มีนาคม 2567");
        PricingGridCommon.DateFactorText("2024-03-05T00:00:00", Identity).Should().Be("5 มีนาคม 2567");
        PricingGridCommon.DateFactorText("2024-03-05T23:30:00", Identity).Should().Be("5 มีนาคม 2567"); // no shift into the next day
        PricingGridCommon.DateFactorText("ไม่ทราบ", Identity).Should().Be("ไม่ทราบ");
        PricingGridCommon.DateFactorText(null, Identity).Should().BeNull();
    }

    [Theory]
    [InlineData("05/03/2024")]      // 5 March or 3 May: not guessed
    [InlineData("05/03/2567")]
    [InlineData("3/5/2024")]
    [InlineData("5 มีนาคม 2567")]
    [InlineData("March 5, 2024")]
    public void A_date_factor_that_is_not_iso_prints_exactly_as_typed(string raw)
        => PricingGridCommon.DateFactorText(raw, Identity).Should().Be(raw);

    [Theory]
    [InlineData("2024-03-04T17:00:00.000Z", "5 มีนาคม 2567")]   // midnight 5 March in Bangkok, as a browser sends it
    [InlineData("2024-03-05T00:00:00+07:00", "5 มีนาคม 2567")]
    [InlineData("2024-03-05T20:00:00Z", "6 มีนาคม 2567")]        // 03:00 the next day there
    [InlineData("2024-03-04 17:00:00Z", "5 มีนาคม 2567")]        // a space instead of the T
    [InlineData("2024-03-04 17:00:00 +00:00", "5 มีนาคม 2567")]
    [InlineData("2024-03-05Z", "5 มีนาคม 2567")]                  // date only, with a zone: midnight UTC is 07:00 there
    [InlineData("2024-03-05+07:00", "5 มีนาคม 2567")]
    [InlineData("2024-03-04T17Z", "5 มีนาคม 2567")]               // hours alone
    [InlineData("2024-03-04T17:00:00+00", "5 มีนาคม 2567")]       // an offset of whole hours
    [InlineData("2024-03-05", "5 มีนาคม 2567")]                    // a plain date ending in "-05" is not an offset
    public void A_date_factor_that_is_an_instant_goes_through_the_application_zone_before_its_date_is_taken(string raw, string expected)
    {
        var bangkok = TimeZoneInfo.CreateCustomTimeZone("app", TimeSpan.FromHours(7), "app", "app");

        PricingGridCommon.DateFactorText(raw, utc => TimeZoneInfo.ConvertTimeFromUtc(utc, bangkok)).Should().Be(expected);
    }

    private static PickedMethod Method(string type, decimal? value, string? role = null, bool selected = false, bool reference = false, int group = 1) => new()
    {
        GroupNumber = group, PricingMethodId = Guid.NewGuid(), MethodType = type, MethodValue = value, Role = role, MethodSelected = selected,
        Context = new PricingMethodContext { IsReference = reference }
    };

    [Fact]
    public void Without_a_stored_value_a_cost_approach_has_none_and_any_other_takes_the_first_valued_method()
    {
        var picked = new[]
        {
            Method("WQS", 1_000m, "Land"), Method("SaleGrid", 1_200m, "Land"),
            Method("BuildingCost", 500m, "Building"), Method("WQS", 0m, "Building")
        };

        ExternalBookBuilder.DeriveApproachValue("Cost", picked).Should().BeNull();   // never a guessed combination
        ExternalBookBuilder.DeriveApproachValue("Market", picked).Should().Be(1_000m); // the first valued method only
        ExternalBookBuilder.DeriveApproachValue("Market", [Method("WQS", 0m)]).Should().BeNull();

        // a stored 0 is "none": it is not printed back, and a stored value wins over any method
        ExternalBookBuilder.ApproachValueOf(0m, "Market", [Method("WQS", 0m)]).Should().BeNull();
        ExternalBookBuilder.ApproachValueOf(0m, "Cost", picked).Should().BeNull();
        ExternalBookBuilder.ApproachValueOf(0m, "Market", picked).Should().Be(1_000m);
        ExternalBookBuilder.ApproachValueOf(7m, "Market", picked).Should().Be(7m);

        // a value derived from one method is that method's alone: the line names it, not every alternative
        ExternalBookBuilder.DerivingMethod("Market", picked).Should().BeSameAs(picked[0]);
        ExternalBookBuilder.DerivingMethod("Cost", picked).Should().BeNull();
    }

    [Fact]
    public void One_machinery_method_is_chosen_per_group_the_non_reference_selected_valued_one_first()
    {
        var reference = Method("MachineryCost", 9m, reference: true, selected: true);
        var unselectedValued = Method("MachineryCost", 5m);
        var selected = Method("MachineryCost", 1m, selected: true);
        var otherGroup = Method("MachineryCost", 2m, group: 2);

        var chosen = CostMachineSectionLoader.OnePerGroup([reference, unselectedValued, selected, otherGroup]).ToList();

        chosen.Should().HaveCount(2);
        chosen.Should().Contain(selected).And.Contain(otherGroup).And.NotContain(reference).And.NotContain(unselectedValued);
        CostMachineSectionLoader.OnePerGroup([Method("MachineryCost", 0m), Method("MachineryCost", 7m)]).Single().MethodValue.Should().Be(7m);
    }

    [Fact]
    public void Duplicate_score_rows_are_reduced_to_one_per_factor_and_comparable_so_every_sum_agrees()
    {
        Guid f = Guid.NewGuid(), c = Guid.NewGuid();
        WqsSectionLoader.ScoreRow R(Guid? cmp, decimal weighted) => new() { FactorId = f, MarketComparableId = cmp, WeightedScore = weighted, Score = weighted };
        var rows = WqsSectionLoader.DistinctScoreRows([R(c, 10m), R(c, 10m), R(null, 4m), R(null, 4m)]);

        rows.Should().HaveCount(2);
        WqsSectionLoader.TotalsRow([f], rows, [c]).ComparableWeighted.Should().Equal("10.00"); // not 20.00
    }
}
