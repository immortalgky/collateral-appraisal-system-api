using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>The letter prints a row of several entries one entry per line, and its ประเภททรัพย์สิน row is the cover's narrative first line.</summary>
public class BookLetterLinesTests
{
    private static Task<string> LetterAsync(AppraisalSummaryModel m) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync("{{ include 'partials/book-letter-external' }}", m, CancellationToken.None);

    // the same rows on the external summary page (its own markup)
    private static Task<string> PageAsync(AppraisalSummaryModel m) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync("{{ include 'partials/book-ext-summary' }}", m, CancellationToken.None);

    [Fact]
    public async Task The_letter_prints_each_entry_of_a_row_on_its_own_line_and_a_single_entry_stays_plain()
    {
        var html = await LetterAsync(new AppraisalSummaryModel
        {
            IsExternal = true,
            LandOwner = "โฉนดที่ดิน 1: นายเอ\nโฉนดที่ดิน 2: นายบี",
            BuildingOwner = "นายเอ",
            BuildingDetailsText = "บ้านเดี่ยว 3 ชั้น แบบ A1 จำนวน 2 หลัง\nโรงจอดรถ 1 ชั้น จำนวน 1 หลัง",
            CondoOwner = "ห้อง 101: นายเอ\nห้อง 102: นายบี",
            ObligationMortgage = "ไม่มี",
            ObligationOthers = "โฉนดที่ดิน 1: ไม่มี\nโฉนดที่ดิน 2: สิทธิเก็บกิน"
        });

        html.Should().Contain("<td class=\"kv-value\"><div>โฉนดที่ดิน 1: นายเอ</div><div>โฉนดที่ดิน 2: นายบี</div></td>")
            .And.Contain("<td class=\"kv-value\"><div>บ้านเดี่ยว 3 ชั้น แบบ A1 จำนวน 2 หลัง</div><div>โรงจอดรถ 1 ชั้น จำนวน 1 หลัง</div></td>")
            .And.Contain("<td class=\"kv-value\"><div>ห้อง 101: นายเอ</div><div>ห้อง 102: นายบี</div></td>")
            .And.Contain("<td class=\"kv-value\"><div>โฉนดที่ดิน 1: ไม่มี</div><div>โฉนดที่ดิน 2: สิทธิเก็บกิน</div></td>")
            .And.Contain("<td class=\"kv-value\">นายเอ</td>").And.Contain("<td class=\"kv-value\">ไม่มี</td>")
            .And.NotContain("นายบี / ").And.NotContain("หลัง, ");
    }

    [Fact]
    public async Task The_letters_property_type_row_is_the_narrative_without_the_covers_area_and_location_lines()
    {
        var m = Standard([new BuildingSection()]);
        m.CoverLines = CoverNarrative.Build(m);
        m.PropertyTypeSummary = CoverNarrative.LetterPropertyType(m);

        var html = await LetterAsync(m);

        html.Should().Contain("<td class=\"kv-value\">ที่ดินพร้อมสิ่งปลูกสร้าง จำนวน 1 รายการ บนที่ดิน</td>").And.NotContain("เนื้อที่ดินรวม").And.NotContain("ตั้งอยู่เลขที่");
    }

    [Fact]
    public async Task A_row_with_several_entries_prints_each_on_its_own_line_and_a_single_entry_stays_plain()
    {
        var html = await PageAsync(new AppraisalSummaryModel
        {
            IsExternal = true,
            LandOwner = "โฉนดที่ดิน 1: นายเอ\nโฉนดที่ดิน 2: นายบี",
            BuildingOwner = "นายเอ",
            CondoOwner = "ห้อง 101: นายเอ\nห้อง 102: นายบี",
            ObligationMortgage = "ไม่มี",
            ObligationOthers = "โฉนดที่ดิน 1: ไม่มี\nโฉนดที่ดิน 2: สิทธิเก็บกิน"
        });

        html.Should().Contain("<span class=\"v\"><div>โฉนดที่ดิน 1: นายเอ</div><div>โฉนดที่ดิน 2: นายบี</div></span>")
            .And.Contain("<span class=\"v\"><div>ห้อง 101: นายเอ</div><div>ห้อง 102: นายบี</div></span>")
            .And.Contain("<span class=\"v\"><div>โฉนดที่ดิน 1: ไม่มี</div><div>โฉนดที่ดิน 2: สิทธิเก็บกิน</div></span>")
            .And.Contain("<span class=\"v\">นายเอ</span>").And.Contain("<span class=\"v\">ไม่มี</span>")
            .And.NotContain("นายบี / ").And.NotContain("หลัง, ");
    }

    [Fact]
    public void The_above_ten_summary_is_one_line_per_group_with_the_pointer_on_the_last()
    {
        var lands = Enumerable.Range(1, 12).Select(i => new LandSection { Titles = [new LandTitleRow { TitleNumber = i.ToString(), TitleTypeDisplay = "โฉนดที่ดิน", OwnerName = i <= 5 ? "นายเอ" : "นายบี" }] });
        var m = new AppraisalSummaryModel { BodyType = "standard", PropertyGroups = [new PropertyGroupDetail { GroupNumber = 1, Lands = lands.ToList() }] };

        var lines = LetterOwnerRows.LandOwners(m)!.Split('\n');

        lines.Should().Equal("นายเอ จำนวน 5 ฉบับ", "นายบี จำนวน 7 ฉบับ (รายละเอียดตามข้อ 2.1)");
    }

    // ── ประเภททรัพย์สิน row ─────────────────────────────────────────────────────────────────────────────────────────

    private static AppraisalSummaryModel Standard(IEnumerable<BuildingSection> buildings) =>
        new() { BodyType = "standard", IsExternal = true, CollateralLocation = "เลขที่ 1", PropertyTypeSummary = "old summary",
                PropertyGroups = [new PropertyGroupDetail { GroupNumber = 1, Lands = [new LandSection { TotalAreaInWa = 100m }], Buildings = buildings.ToList() }] };

    [Fact]
    public void The_property_type_row_is_the_first_cover_line_for_land_with_buildings_and_for_a_condo()
    {
        var lb = Standard([new BuildingSection(), new BuildingSection()]);
        lb.CoverLines = CoverNarrative.Build(lb);
        CoverNarrative.LetterPropertyType(lb).Should().Be("ที่ดินพร้อมสิ่งปลูกสร้าง จำนวน 2 รายการ บนที่ดิน");

        var condo = new AppraisalSummaryModel { BodyType = "condo", IsExternal = true, PropertyTypeSummary = "old summary", CondoSection = new CondoSection { Units = [new CondoUnitAreaRow(), new CondoUnitAreaRow()] } };
        condo.CoverLines = CoverNarrative.Build(condo);
        CoverNarrative.LetterPropertyType(condo).Should().Be("ห้องชุด จำนวน 2 ห้อง");
    }

    [Fact]
    public void A_body_type_the_cover_does_not_describe_keeps_the_builders_summary()
    {
        var other = Standard([]);
        other.BodyType = "lease";

        other.CoverLines = CoverNarrative.Build(other);
        CoverNarrative.LetterPropertyType(other).Should().Be("old summary");
    }

    [Fact]
    public void A_machinery_only_appraisal_now_gets_the_narrative_row_on_the_letter()
    {
        var m = new AppraisalSummaryModel { BodyType = "machine", IsExternal = true, PropertyTypeSummary = "old summary", MachineSection = new MachineSection { Machines = [new MachineRow(), new MachineRow()] } };

        m.CoverLines = CoverNarrative.Build(m);
        CoverNarrative.LetterPropertyType(m).Should().Be("เครื่องจักรและอุปกรณ์ จำนวน 2 รายการ");
    }

    [Fact]
    public async Task The_summary_page_row_prints_the_narrative_without_the_cover_s_area_and_location_lines()
    {
        var m = Standard([new BuildingSection()]);
        m.CoverLines = CoverNarrative.Build(m);
        m.PropertyTypeSummary = CoverNarrative.LetterPropertyType(m);

        var html = await PageAsync(m);

        html.Should().Contain("<span class=\"l\">ประเภททรัพย์สิน</span><span class=\"c\">:</span><span class=\"v\">ที่ดินพร้อมสิ่งปลูกสร้าง จำนวน 1 รายการ บนที่ดิน</span>").And.NotContain("เนื้อที่ดินรวม").And.NotContain("ตั้งอยู่เลขที่");
    }
}
