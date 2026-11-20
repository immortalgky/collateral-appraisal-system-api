using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>The cover's property block: up to three centred lines (what it is / how big / where) instead of the "ประเภททรัพย์สิน :" and "ที่ตั้งทรัพย์สิน :" lines.</summary>
public class BookCoverNarrativeTests
{
    private const string Address = "เลขที่ 12/3 หมู่ที่ 4 ตำบลตัวอย่าง อำเภอตัวอย่าง จังหวัดตัวอย่าง";

    // a development-only property's rows are all ส่วนพัฒนา items, each carrying the owning property's IsAppraisable (as BuildingSectionLoader sets it)
    private static BuildingSection Bld(bool? appraisable = null, bool dev = false) =>
        new() { IsAppraisable = appraisable, IsDevelopmentOnly = dev, DevelopmentItems = dev ? [new DevelopmentItem { IsAppraisable = appraisable }] : [] };

    private static AppraisalSummaryModel Model(string? body = "standard", bool external = false, decimal landWa = 0m, IEnumerable<BuildingSection>? buildings = null, bool land = true,
        string? address = Address, CondoSection? condo = null, string? propertyType = null, string? summary = null)
    {
        var m = new AppraisalSummaryModel
        {
            BodyType = body, IsExternal = external, CondoSection = condo, PropertyType = propertyType, PropertyTypeSummary = summary,
            PropertyGroups =
            [
                new PropertyGroupDetail
                {
                    GroupNumber = 1,
                    Lands = land ? [new LandSection { TotalAreaInWa = landWa }] : [],
                    Buildings = (buildings ?? []).ToList()
                }
            ]
        };
        if (external) m.CollateralLocation = address; else m.CollateralAddress = address;
        return m;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Land_with_buildings_and_development_items_prints_all_three_lines(bool external)
    {
        var lines = CoverNarrative.Build(Model(external: external, landWa: 1066m, buildings: [Bld(), Bld(true), Bld(dev: true)]));

        lines.Should().Equal(
            "ที่ดินพร้อมสิ่งปลูกสร้าง จำนวน 2 รายการ และส่วนพัฒนาอื่นๆ บนที่ดิน",
            "เนื้อที่ดินรวม 2 ไร่ 2 งาน 66 ตารางวา (หรือ 1,066 ตารางวา)",
            "ตั้งอยู่เลขที่ 12/3 หมู่ที่ 4 ตำบลตัวอย่าง อำเภอตัวอย่าง จังหวัดตัวอย่าง");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Land_with_buildings_and_no_development_items_has_no_and_clause(bool external)
    {
        var lines = CoverNarrative.Build(Model(external: external, landWa: 43.33m, buildings: [Bld(), Bld(), Bld()]));

        lines[0].Should().Be("ที่ดินพร้อมสิ่งปลูกสร้าง จำนวน 3 รายการ บนที่ดิน");
        lines[1].Should().Be("เนื้อที่ดินรวม 43.33 ตารางวา");
    }

    [Fact]
    public void A_not_appraised_building_and_a_not_appraised_development_item_are_left_out()
    {
        var lines = CoverNarrative.Build(Model(landWa: 100m, buildings: [Bld(), Bld(false), Bld(false, dev: true)]));

        lines[0].Should().Be("ที่ดินพร้อมสิ่งปลูกสร้าง จำนวน 1 รายการ บนที่ดิน");
    }

    [Fact]
    public void A_non_building_row_of_a_real_building_counts_as_ส่วนพัฒนา_like_3_2_and_one_of_a_not_appraised_building_does_not()
    {
        var withFence = new BuildingSection { DevelopmentItems = [new DevelopmentItem { AreaDescription = "รั้ว" }] };   // a real building that also has a fence row
        var notAppraised = new BuildingSection { IsAppraisable = false, DevelopmentItems = [new DevelopmentItem { IsAppraisable = false }] };

        CoverNarrative.Build(Model(landWa: 100m, buildings: [withFence]))[0].Should().Be("ที่ดินพร้อมสิ่งปลูกสร้าง จำนวน 1 รายการ และส่วนพัฒนาอื่นๆ บนที่ดิน");
        CoverNarrative.Build(Model(landWa: 100m, buildings: [Bld(), notAppraised]))[0].Should().Be("ที่ดินพร้อมสิ่งปลูกสร้าง จำนวน 1 รายการ บนที่ดิน");
        CoverNarrative.Build(Model(land: false, buildings: [withFence]))[0].Should().Be("สิ่งปลูกสร้าง จำนวน 1 รายการ และส่วนพัฒนาอื่นๆ");
    }

    [Fact]
    public void Land_only_is_plain_land_because_the_book_has_no_vacancy_flag()
    {
        CoverNarrative.Build(Model(landWa: 400m)).Take(2).Should().Equal("ที่ดิน", "เนื้อที่ดินรวม 1 ไร่ 0 งาน 0 ตารางวา (หรือ 400 ตารางวา)");
        CoverNarrative.Build(Model(landWa: 400m, buildings: [Bld(false)]))[0].Should().Be("ที่ดิน");
    }

    [Fact]
    public void Buildings_without_land_print_the_count_only()
    {
        var lines = CoverNarrative.Build(Model(land: false, buildings: [Bld(), Bld(), Bld(dev: true)]));

        lines.Should().Equal("สิ่งปลูกสร้าง จำนวน 2 รายการ และส่วนพัฒนาอื่นๆ", "ตั้งอยู่เลขที่ 12/3 หมู่ที่ 4 ตำบลตัวอย่าง อำเภอตัวอย่าง จังหวัดตัวอย่าง");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Condo_prints_the_room_count_and_the_usable_area(bool external)
    {
        var condo = new CondoSection { Units = [new CondoUnitAreaRow(), new CondoUnitAreaRow()], UsableArea = 71.5m };
        var lines = CoverNarrative.Build(Model("condo", external, land: false, condo: condo));

        lines[0].Should().Be("ห้องชุด จำนวน 2 ห้อง");
        lines[1].Should().Be("เนื้อที่ห้องชุดรวม 71.5 ตารางเมตร");
        lines[2].Should().StartWith("ตั้งอยู่เลขที่ ");
    }

    [Fact]
    public void An_unknown_size_skips_line_2()
    {
        CoverNarrative.Build(Model(landWa: 0m, buildings: [Bld()])).Should().HaveCount(2).And.NotContain(l => l.StartsWith("เนื้อที่"));
        CoverNarrative.Build(Model("condo", land: false, condo: new CondoSection { Units = [new CondoUnitAreaRow()] })).Should().NotContain(l => l.StartsWith("เนื้อที่"));
    }

    [Fact]
    public void An_address_that_does_not_start_with_the_house_number_word_gets_a_space_and_none_means_no_line_3()
    {
        CoverNarrative.Build(Model(landWa: 100m, address: "หมู่ที่ 4 ตำบลตัวอย่าง"))[^1].Should().Be("ตั้งอยู่ หมู่ที่ 4 ตำบลตัวอย่าง");
        CoverNarrative.Build(Model(landWa: 100m, address: "-")).Should().HaveCount(2);
        CoverNarrative.Build(Model(landWa: 100m, address: null)).Should().HaveCount(2);
    }

    [Theory]
    [InlineData("block")]            // a block book with no project section
    [InlineData("construction")]     // a construction inspection with no buildings
    [InlineData("lease")]
    [InlineData(null)]
    public void Other_body_types_and_incomplete_sections_return_no_lines(string? body) => CoverNarrative.Build(Model(body, landWa: 100m)).Should().BeEmpty();

    // ── machinery ───────────────────────────────────────────────────────────────────────────────────────────────────

    private static MachineSection Machines(int rows) => new() { Machines = Enumerable.Range(1, rows).Select(i => new MachineRow { Sequence = i, Quantity = 10 }).ToList() };

    [Fact]
    public void Machinery_only_counts_the_rows_not_the_quantities_and_uses_the_machine_address()
    {
        var m = Model("machine", land: false, address: "เลขที่ 99 นิคมอุตสาหกรรมตัวอย่าง");
        m.MachineSection = Machines(3);

        CoverNarrative.Build(m).Should().Equal("เครื่องจักรและอุปกรณ์ จำนวน 3 รายการ", "ตั้งอยู่เลขที่ 99 นิคมอุตสาหกรรมตัวอย่าง");
    }

    // ── mixed families ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Land_and_condo_join_the_phrases_and_the_sizes_with_และ_and_keep_both_addresses()
    {
        var condo = new CondoSection { Units = [new CondoUnitAreaRow()], UsableArea = 35.5m };
        var m = Model(landWa: 1066m, buildings: [Bld()], address: "เลขที่ 12/3 ตำบลตัวอย่าง\nห้องชุดเลขที่ 101 อาคารตัวอย่าง", condo: condo);

        CoverNarrative.Build(m).Should().Equal(
            "ที่ดินพร้อมสิ่งปลูกสร้าง จำนวน 1 รายการ บนที่ดิน และห้องชุด จำนวน 1 ห้อง",
            "เนื้อที่ดินรวม 2 ไร่ 2 งาน 66 ตารางวา (หรือ 1,066 ตารางวา) และเนื้อที่ห้องชุดรวม 35.5 ตารางเมตร",
            "ตั้งอยู่เลขที่ 12/3 ตำบลตัวอย่าง\nห้องชุดเลขที่ 101 อาคารตัวอย่าง");
    }

    [Fact]
    public void Land_condo_and_machinery_together_and_only_the_sizes_that_exist()
    {
        var m = Model(landWa: 0m, land: true, condo: new CondoSection { Units = [new CondoUnitAreaRow(), new CondoUnitAreaRow()] });
        m.MachineSection = Machines(2);

        var lines = CoverNarrative.Build(m);

        lines[0].Should().Be("ที่ดิน และห้องชุด จำนวน 2 ห้อง และเครื่องจักรและอุปกรณ์ จำนวน 2 รายการ");
        lines.Should().NotContain(l => l.StartsWith("เนื้อที่"));
    }

    [Fact]
    public void Condo_and_machinery_without_land_read_as_two_phrases()
    {
        var m = Model("standard", land: false, condo: new CondoSection { Units = [new CondoUnitAreaRow()], UsableArea = 40m });
        m.MachineSection = Machines(1);

        CoverNarrative.Build(m).Take(2).Should().Equal("ห้องชุด จำนวน 1 ห้อง และเครื่องจักรและอุปกรณ์ จำนวน 1 รายการ", "เนื้อที่ห้องชุดรวม 40 ตารางเมตร");
    }

    // ── block (project) ─────────────────────────────────────────────────────────────────────────────────────────────

    private static AppraisalSummaryModel BlockModel(bool condo, int units = 48, string? name = "ตัวอย่างวิลล่า", decimal? wa = 1200m, string? address = "เลขที่ 5 ถนนตัวอย่าง ตำบลตัวอย่าง", bool external = false) =>
        new()
        {
            BodyType = "block", IsExternal = external, ProjectAddress = address,
            BlockSection = new BlockBookSection { IsCondo = condo, Project = new BlockProjectDetail { ProjectName = name, AppraisedUnitCount = units, LandAreaSquareWa = wa } }
        };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_house_project_prints_the_units_the_project_land_and_the_project_address(bool external)
    {
        CoverNarrative.Build(BlockModel(false, external: external)).Should().Equal(
            "ที่ดินพร้อมสิ่งปลูกสร้าง จำนวน 48 หน่วย ในโครงการ ตัวอย่างวิลล่า",
            "เนื้อที่ดินโครงการรวม 3 ไร่ 0 งาน 0 ตารางวา (หรือ 1,200 ตารางวา)",
            "ตั้งอยู่เลขที่ 5 ถนนตัวอย่าง ตำบลตัวอย่าง");
    }

    [Fact]
    public void A_condo_project_has_no_area_line_and_the_name_part_is_dropped_when_it_is_not_stated()
    {
        CoverNarrative.Build(BlockModel(true, units: 120)).Should().Equal("ห้องชุด จำนวน 120 ห้อง ในโครงการ ตัวอย่างวิลล่า", "ตั้งอยู่เลขที่ 5 ถนนตัวอย่าง ตำบลตัวอย่าง");
        CoverNarrative.Build(BlockModel(false, name: "-", wa: null))[0].Should().Be("ที่ดินพร้อมสิ่งปลูกสร้าง จำนวน 48 หน่วย");
        CoverNarrative.Build(BlockModel(false, address: null)).Should().HaveCount(2);
    }

    [Fact]
    public void A_project_with_no_appraised_unit_falls_back_to_the_model_unit_counts_and_then_to_the_old_lines()
    {
        var m = BlockModel(false, units: 0);
        m.BlockSection = new BlockBookSection { Project = m.BlockSection!.Project, Models = [new BlockModelDetail { UnitCount = 30 }, new BlockModelDetail { UnitCount = 12 }] };

        CoverNarrative.Build(m)[0].Should().StartWith("ที่ดินพร้อมสิ่งปลูกสร้าง จำนวน 42 หน่วย");
        CoverNarrative.Build(BlockModel(false, units: 0)).Should().BeEmpty();
    }

    // ── construction inspection ─────────────────────────────────────────────────────────────────────────────────────

    private static AppraisalSummaryModel Construction(int buildings, decimal? pct = 40.4m, string? round = "3", bool land = true, decimal wa = 400m)
    {
        var m = new AppraisalSummaryModel
        {
            BodyType = "construction", CollateralAddress = Address,
            ConstructionSection = new ConstructionSection { CurrentInspectionRound = round, Buildings = Enumerable.Range(1, buildings).Select(_ => new ConstructionBuilding { TotalCurrentPct = 10m }).ToList() },
            PropertyGroups = [new PropertyGroupDetail { GroupNumber = 1, Lands = land ? [new LandSection { TotalAreaInWa = wa }] : [], Buildings = Enumerable.Range(1, buildings).Select(_ => new BuildingSection { ConstructionProgressPct = pct }).ToList() }]
        };
        return m;
    }

    [Fact]
    public void A_single_building_under_inspection_shows_the_round_and_the_cumulative_progress()
    {
        CoverNarrative.Build(Construction(1)).Should().Equal(
            "ที่ดินพร้อมสิ่งปลูกสร้างระหว่างก่อสร้าง จำนวน 1 รายการ บนที่ดิน",
            "เนื้อที่ดินรวม 1 ไร่ 0 งาน 0 ตารางวา (หรือ 400 ตารางวา)",
            "ตรวจผลงานก่อสร้าง งวดที่ 3 ผลงานสะสม 40.4%",
            "ตั้งอยู่เลขที่ 12/3 หมู่ที่ 4 ตำบลตัวอย่าง อำเภอตัวอย่าง จังหวัดตัวอย่าง");
    }

    [Theory]
    [InlineData(99.6, "99.6")]    // the figure as stored, up to 2 decimals, trailing zeros dropped
    [InlineData(65, "65")]
    [InlineData(59.5, "59.5")]
    [InlineData(99.99, "99.99")]
    [InlineData(99.9999, "99.99")] // the column holds 4 decimals: cut, never rounded up to 100 on an unfinished building
    [InlineData(100, "100")]
    public void The_progress_uses_the_same_display_rule_as_the_building_table(double pct, string shown) =>
        CoverNarrative.Build(Construction(1, (decimal)pct))[2].Should().Be($"ตรวจผลงานก่อสร้าง งวดที่ 3 ผลงานสะสม {shown}%");

    [Fact]
    public void Several_buildings_drop_the_percentage_and_no_round_drops_the_round()
    {
        CoverNarrative.Build(Construction(3))[0].Should().Be("ที่ดินพร้อมสิ่งปลูกสร้างระหว่างก่อสร้าง จำนวน 3 รายการ บนที่ดิน");
        CoverNarrative.Build(Construction(3))[2].Should().Be("ตรวจผลงานก่อสร้าง งวดที่ 3");
        CoverNarrative.Build(Construction(1, round: " - "))[2].Should().Be("ตรวจผลงานก่อสร้าง ผลงานสะสม 40.4%");
        CoverNarrative.Build(Construction(2, round: null))[2].Should().Be("ตรวจผลงานก่อสร้าง");
    }

    [Fact]
    public void A_construction_inspection_without_land_says_buildings_only_and_has_no_area_line()
    {
        var lines = CoverNarrative.Build(Construction(2, land: false));

        lines.Should().Equal("สิ่งปลูกสร้างระหว่างก่อสร้าง จำนวน 2 รายการ", "ตรวจผลงานก่อสร้าง งวดที่ 3", $"ตั้งอยู่{Address}");
    }

    [Fact]
    public void Without_a_progress_figure_for_the_single_building_the_inspection_total_stands_in()
    {
        CoverNarrative.Build(Construction(1, pct: null))[2].Should().Be("ตรวจผลงานก่อสร้าง งวดที่ 3 ผลงานสะสม 10%");
    }

    private static Task<string> RenderAsync(string partial, AppraisalSummaryModel m) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync($"{{{{ include 'partials/{partial}' }}}}", m, CancellationToken.None);

    [Theory]
    [InlineData("book-cover-internal", false)]
    [InlineData("book-cover-external", true)]
    public async Task The_cover_prints_the_lines_centred_without_a_label_prefix(string partial, bool external)
    {
        var m = Model(external: external, landWa: 1066m, buildings: [Bld()], propertyType: "old type", summary: "old summary");
        m.CoverLines = CoverNarrative.Build(m);

        var html = await RenderAsync(partial, m);

        html.Should().Contain("<div class=\"cover-property-type\" style=\"white-space:pre-line\">ที่ดินพร้อมสิ่งปลูกสร้าง จำนวน 1 รายการ บนที่ดิน</div>")
            .And.Contain("<div class=\"cover-location\" style=\"white-space:pre-line\">ตั้งอยู่เลขที่ 12/3")
            .And.NotContain("ประเภททรัพย์สิน :").And.NotContain("ที่ตั้งทรัพย์สิน :").And.NotContain("old summary");
    }

    [Theory]
    [InlineData("book-cover-internal", false, "ประเภททรัพย์สิน : เครื่องจักร")]
    [InlineData("book-cover-external", true, "ประเภททรัพย์สิน : เครื่องจักร")]
    public async Task A_body_type_without_lines_keeps_the_old_two_lines(string partial, bool external, string expected)
    {
        var m = Model(null, external, landWa: 0m, propertyType: "เครื่องจักร", summary: "เครื่องจักร");
        m.CoverLines = CoverNarrative.Build(m);

        var html = await RenderAsync(partial, m);

        html.Should().Contain(expected).And.Contain("ที่ตั้งทรัพย์สิน : ").And.NotContain("ตั้งอยู่");
    }
}
